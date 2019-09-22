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
[<Route("api/[controller]")>]
[<ApiController>]
type AccountController () =
    inherit ControllerBase()

    [<HttpPost("logout")>]
    member this.Logout() =
        base.SignOut(CookieAuthenticationDefaults.AuthenticationScheme)