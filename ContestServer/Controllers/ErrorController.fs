namespace ContestServer.Controllers

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Authorization
[<CLIMutable>]
type ErrorStatus =
    {
        code:Int16
        result:string
        url:string
    }

[<Route("api/[controller]")>]
[<ApiController>]
[<AllowAnonymous>]
type ErrorController () =
    inherit ControllerBase()

    [<HttpGet("AccessDenied")>]
    member this.AccessDenied (returnurl:string)=
        let ret = {
            code = 403s
            result = "AccessDenied"
            url = returnurl
        }
        base.StatusCode(403,ret)
        

