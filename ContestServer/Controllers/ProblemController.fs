namespace ContestServer.Controllers

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Authorization
open Microsoft.Extensions.Logging
open ContestServer.Setting
open ContestServer.Database2Data.Problem
[<Route("api/[controller]")>]
[<ApiController>]
[<Authorize(Policy = "UserOnly")>]
type ProblemController (logger : ILogger<ProblemController>) =
    inherit ControllerBase()

    [<HttpGet>]
    member __.Get() =
        let elms = getProblems ()
        base.Ok(elms)
    [<HttpGet("{contestId}")>]
    member __.Get(contestId:int) =
        let elms = getProblemsOfContest contestId
        base.Ok(elms)


        