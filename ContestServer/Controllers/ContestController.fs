namespace ContestServer.Controllers

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Authorization
open Microsoft.Extensions.Logging
open ContestServer.Setting
open ContestServer.Database2Data.Contest
[<Route("api/[controller]")>]
[<ApiController>]
[<Authorize(Policy = "UserOnly")>]
type ContestController (logger : ILogger<ContestController>) =
    inherit ControllerBase()

    [<HttpGet>]
    member __.Get() =
        let elms = getContests ()
        base.Ok(elms)

        