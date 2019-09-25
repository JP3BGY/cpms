namespace ContestServer.Controllers

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Authorization
open Microsoft.Extensions.Logging
open ContestServer.Types

[<Route("api/[controller]")>]
[<ApiController>]
[<AllowAnonymous>]
type ErrorController (logger : ILogger<ErrorController>) =
    inherit ControllerBase()

    [<HttpGet("AccessDenied")>]
    member __.AccessDenied (returnurl:string)=
        let ret = {
            code = 403s
            result = "AccessDenied"
            url = returnurl
        }
        base.StatusCode(403,ret)
        

