namespace ContestServer.Controllers

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.OAuth
open Microsoft.AspNetCore.Authorization
open ContestServer.Setting
open ContestServer.Database2Data.Contest
[<Route("/[controller]")>]
[<ApiController>]
type AccountController () =
    inherit ControllerBase()

    [<HttpGet("login")>]
    member this.Login() =
        let authProp = AuthenticationProperties()
        authProp.RedirectUri<-this.Url.Content("~/")
        base.Challenge(authProp,"GitHub")
    [<HttpPost("login")>]
    member this.LoginWithRedirect([<FromBody>]redirectUrl:string) =
        eprintfn "redirect Url %s" redirectUrl
        let authProp = AuthenticationProperties()
        authProp.RedirectUri<-this.Url.Content(redirectUrl)
        base.Challenge(authProp,"GitHub")
    [<HttpPost("logout")>]
    member this.Logout() =
        base.SignOut(CookieAuthenticationDefaults.AuthenticationScheme)