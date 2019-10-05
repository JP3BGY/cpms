namespace ContestServer

open System
open System.Text.Json
open System.Transactions
open System.Security.Claims
open System.Net.Http
open System.Net.Http.Headers
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.HttpsPolicy;
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Headers
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.OAuth
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Primitives
open FSharp.Data

open ContestServer.Setting

type Orgs = JsonProvider<"../data/github_orgs.json">
type Teams = JsonProvider<"../data/github_teams.json">
type Oauth2User = JsonProvider<"../data/github_user.json">
type Emails = JsonProvider<"../data/github_emails.json">
type Startup private () =
    new (configuration: IConfiguration) as this =
        Startup() then
        this.Configuration <- configuration

    // This method gets called by the runtime. Use this method to add services to the container.
    member this.ConfigureServices(services: IServiceCollection) =
        // Add framework services.
        services.AddAuthentication(fun options ->
                options.DefaultScheme <- CookieAuthenticationDefaults.AuthenticationScheme
                options.DefaultChallengeScheme <- "GitHub"
                ())
            .AddCookie(
                fun options ->
                    options.Cookie.Name<-"github.auth"
                    options.Cookie.SecurePolicy <- CookieSecurePolicy.Always
                    options.AccessDeniedPath <- PathString("/api/Error/AccessDenied")
                    ()
            )
            .AddOAuth("GitHub",
                fun (options:OAuthOptions) ->
                    options.ClientId <- this.Configuration.["GitHub:ClientId"]
                    options.ClientSecret <- this.Configuration.["GitHub:ClientSecret"]
                    options.Scope.Add("read:org user:email")
                    options.CallbackPath <- PathString("/signin-github")
                    options.AuthorizationEndpoint <- "https://github.com/login/oauth/authorize"
                    options.TokenEndpoint <- "https://github.com/login/oauth/access_token"
                    options.UserInformationEndpoint <- "https://api.github.com/user"

                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier,"id")
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name,"name")
                    options.ClaimActions.MapJsonKey("github:login","login")
                    options.Events <- OAuthEvents
                        (
                            OnRedirectToAuthorizationEndpoint = fun context ->
                                            async{
                                                eprintfn "On Redirect To Login"
                                                let requestHeaders = RequestHeaders(context.Request.Headers);
                                                if (requestHeaders.Accept.Any(fun acceptValue -> 
                                                                                    acceptValue.MediaType.Value.Equals("text/html", 
                                                                                        StringComparison.OrdinalIgnoreCase))) then
                                                    context.Response.Redirect(context.RedirectUri);
                                                else
                                                    context.Response.Headers.["Location"] <- StringValues context.RedirectUri;
                                                    context.Response.StatusCode <- 401;
                                            }|>Async.StartAsTask:>Task
                            ,OnCreatingTicket = fun context ->
                                async{
                                    let httpGetWithToken (url:string) = 
                                        let request = new HttpRequestMessage (System.Net.Http.HttpMethod.Get ,url)
                                        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue("application/json"))
                                        request.Headers.Authorization <- AuthenticationHeaderValue("Bearer",context.AccessToken)
                                        let response = 
                                            context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted)
                                                |>Async.AwaitTask|>Async.RunSynchronously
                                        response.EnsureSuccessStatusCode()|>ignore
                                        let raw = response.Content.ReadAsStringAsync()|>Async.AwaitTask|>Async.RunSynchronously
                                        raw
                                    let userRaw = httpGetWithToken context.Options.UserInformationEndpoint
                                    eprintfn "user: %s" userRaw
                                    let user = JsonDocument.Parse(userRaw)
                                    context.RunClaimActions(user.RootElement)
                                    let userJson = Oauth2User.Parse(userRaw)
                                    let emailsRaw = httpGetWithToken "https://api.github.com/user/emails"
                                    let emailJson = Emails.Parse(emailsRaw)
                                    let primaryEmail = 
                                        emailJson
                                        |> Array.filter(
                                            fun email -> 
                                                email.Primary
                                        )
                                        |>Array.head
                                        |>fun x -> 
                                            x.Email
                                    let ctx = 
                                        getDataContext()
                                    let contestServerUser = 
                                        query{
                                            for user in ctx.ContestLog.User do
                                                where (user.UserLogin = userJson.Login)
                                                select user
                                        }|>Seq.toArray
                                    let isNotUser = Array.isEmpty contestServerUser
                                    if isNotUser then
                                        let authorizedOrgs = this.Configuration.GetSection("GitHub:AuthorizedOrgs").GetChildren().Select(fun x -> x.Value).AsEnumerable()|>Seq.toArray
                                        authorizedOrgs 
                                            |> Array.map(
                                                fun x ->
                                                    let orgTeam = x.Split [|':'|]
                                                    if Array.length orgTeam = 1 then
                                                        let restext = httpGetWithToken "https://api.github.com/user/orgs"
                                                        let orgs = 
                                                            Orgs.Parse(restext)
                                                                |> Seq.filter(fun org -> org.Login = orgTeam.[0])
                                                        not (Seq.isEmpty orgs)
                                                    else
                                                        if Array.length orgTeam = 2 then
                                                            let restext = httpGetWithToken "https://api.github.com/user/teams"
                                                            let teams = 
                                                                Teams.Parse(restext)
                                                                    |> Seq.filter(fun org -> org.Name = orgTeam.[1] && org.Organization.Login = orgTeam.[0])
                                                            not (Seq.isEmpty teams)
                                                        else
                                                            false

                                            )
                                            |>Array.exists (id)
                                            |>
                                                fun x ->
                                                    if x then 
                                                        context.Identity.AddClaim(Claim("Permission","User"))
                                                        try 
                                                            let transactionopt = TransactionOptions(Timeout=TimeSpan.Zero,IsolationLevel=IsolationLevel.Serializable)
                                                            use transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                                                                                                   transactionopt,
                                                                                                   TransactionScopeAsyncFlowOption.Enabled)
                                                            let elm = ctx.ContestLog.User.``Create(userEmail, userLogin)``(primaryEmail,userJson.Login)
                                                            ctx.SubmitUpdates()
                                                            transaction.Complete()
                                                        with
                                                        | :? TransactionAbortedException as te ->
                                                            eprintfn "Transaction Error: Can't Create User \n      %s" te.Message
                                                            GC.Collect()
                                                        ()
                                                    else 
                                                        ()
                                    else 
                                        context.Identity.AddClaim(Claim("Permission","User"))
                                    
                                }|>Async.StartAsTask:>Task
                        )
                    ()
            )|>ignore
        services.AddAuthorization(
            fun options ->
                options.AddPolicy("UserOnly",
                    fun policy ->
                        policy.RequireClaim("Permission","User")|>ignore
                        ())
                ()
        )|>ignore
        services.AddControllers() |> ignore

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member this.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        if (env.IsDevelopment()) then
            app.UseDeveloperExceptionPage() |> ignore
        
        app.UseHttpsRedirection() |> ignore
        app.UseStaticFiles() |> ignore
        app.UseRouting() |> ignore

        app.UseAuthentication() |> ignore
        app.UseAuthorization() |> ignore

        app.UseEndpoints(fun endpoints ->
            endpoints.MapControllerRoute("client","/{*path}",{|controller="Spa";action="Get"|}) |> ignore
            endpoints.MapControllers() |> ignore
            ) |> ignore

    member val Configuration : IConfiguration = null with get, set
