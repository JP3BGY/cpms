namespace ContestServer.Controllers

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.OAuth
open Microsoft.AspNetCore.Authorization
open Microsoft.Extensions.Logging
open ContestServer.Setting
open ContestServer.Database2Data.Contest
[<Route("/[controller]")>]
[<ApiController>]
type AccountController (logger : ILogger<AccountController>) =
    inherit ControllerBase()

    [<HttpGet("login")>]
    member __.Login() =
        let authProp = AuthenticationProperties()
        authProp.RedirectUri<-__.Url.Content("~/")
        base.Challenge(authProp,"GitHub")
    [<HttpPost("login")>]
    member __.LoginWithRedirect([<FromBody>]redirectUrl:string) =
        eprintfn "redirect Url %s" redirectUrl
        let authProp = AuthenticationProperties()
        authProp.RedirectUri<-__.Url.Content(redirectUrl)
        base.Challenge(authProp,"GitHub")
    [<HttpPost("logout")>]
    member __.Logout() =
        base.SignOut(CookieAuthenticationDefaults.AuthenticationScheme)