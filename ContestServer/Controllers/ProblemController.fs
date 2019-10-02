namespace ContestServer.Controllers

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Authorization
open Microsoft.Extensions.Logging
open ContestServer.Setting
open ContestServer.Types
open ContestServer.Database2Data.Problem
[<Route("api/[controller]")>]
[<ApiController>]
[<Authorize(Policy = "UserOnly")>]
type ProblemController (logger : ILogger<ProblemController>) =
    inherit ControllerBase()

    [<HttpGet>]
    member __.Get([<FromQuery>]offset,[<FromQuery>]n,[<FromQuery>]search) =
        eprintfn "search %s" search
        if offset<0||n<0 || n>200 then
            base.BadRequest({
            code = 400s
            result = "offset and n must be positive integer and smaller than 201"
            url = null
            }):>ActionResult
        else 
            let elms = 
                getLimitProblems offset n 
            let ctx = getDataContext()
            let elmnum=
                query{
                    for problem in ctx.ContestLog.Problem do
                        count
                }
            base.Ok({problems=elms|>Seq.toArray;elmNum=elmnum}):>ActionResult
    [<HttpGet("{contestId}")>]
    member __.Get(contestId:int) =
        let elms = getProblemsOfContest contestId
        base.Ok(elms)


        