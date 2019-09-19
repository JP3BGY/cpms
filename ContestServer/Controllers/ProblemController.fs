namespace ContestServer.Controllers

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Authorization
open ContestServer.Setting
open ContestServer.Database2Data.Problem
[<Route("api/[controller]")>]
[<ApiController>]
[<Authorize(Policy = "UserOnly")>]
type ProblemController () =
    inherit ControllerBase()

    [<HttpGet>]
    member this.Get() =
        let elms = getProblems ()
        base.Ok(elms)
    [<HttpGet("{contestId}")>]
    member this.Get(contestId:int) =
        let elms = getProblemsOfContest contestId
        base.Ok(elms)


        