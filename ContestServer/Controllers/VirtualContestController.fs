namespace ContestServer.Controllers

open System
open System.Transactions
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Authorization
open ContestServer.Setting
open ContestServer.VirtualContest
open ContestServer.Database2Data.UserInfo
open ContestServer.Types
open ContestServer.Database2Data.Problem
open FSharp.Data
[<Route("api/[controller]")>]
[<ApiController>]
[<Authorize(Policy = "UserOnly")>]
type VirtualContestController (logger : ILogger<VirtualContestController>) =
    inherit ControllerBase()
    [<HttpGet>]
    member __.GetVirtualContests() =
        let elms = getVirtualContests ()
        base.Ok(elms)
    [<HttpGet("details/{id}")>]
    member __.GetDetails(id) =
        let userInfo = getUserInfoFromControllerBase (__:>ControllerBase)
        let res = getProblemsOfVirtualContest id (getUserInfoFromControllerBase (__:>ControllerBase))
        match res with
        | Ok (vcon,probs,subs)->
            base.Ok({
                vContest = vcon
                problems=probs
                submissions=subs
                isCreator = vcon.creator.dbId = userInfo.dbId
            }):>ActionResult
        | Error err ->
            let res = 
                {
                    code=500s
                    result = err
                    url = null
                }
            __.StatusCode(500,res):>ActionResult

    [<HttpPost("create")>]
    member __.Create([<FromBody>] setting) =
        let duration = setting.duration
        let problemIds = setting.problems
        let startTime = setting.startTime
        let userInfo = getUserInfoFromControllerBase (__:>ControllerBase)
        if System.Text.ASCIIEncoding.Unicode.GetByteCount(setting.name) > 250 then  
            let res = 
                {
                    code=500s
                    result = "name is too long"
                    url = null
                }
            __.StatusCode(500,res):>ActionResult
        else 
            let result = createContest userInfo startTime duration problemIds (setting.name)
            match result with
            | Ok x -> __.Ok(x):>ActionResult
            | Error err -> 
                let res = 
                    {
                        code=500s
                        result = err
                        url = null
                    }
                __.StatusCode(500,res):>ActionResult
    [<HttpPost("modify/{id}")>]
    member __.Modify(id:int,[<FromBody>] setting) =
        let duration = setting.duration
        let problemIds = setting.problems
        let startTime = setting.startTime
        let userInfo = getUserInfoFromControllerBase (__:>ControllerBase)
        if System.Text.ASCIIEncoding.Unicode.GetByteCount(setting.name) > 250 then  
            let res = 
                {
                    code=500s
                    result = "name is too long"
                    url = null
                }
            __.StatusCode(500,res):>ActionResult
        else 
            let result = modifyContest id userInfo startTime duration problemIds (setting.name)
            match result with
            | Ok x -> __.Ok(x):>ActionResult
            | Error err -> 
                let res = 
                    {
                        code=500s
                        result = err
                        url = null
                    }
                __.StatusCode(500,res):>ActionResult
    [<HttpPost("delete/{id}")>]
    member __.Delete(id:int) =
        let userInfo = getUserInfoFromControllerBase (__:>ControllerBase)
        let result = deleteContest userInfo id
        match result with
        | Ok x -> __.Ok(x):>ActionResult
        | Error err -> 
            let res = 
                {
                    code=500s
                    result = err
                    url = null
                }
            __.StatusCode(500,res):>ActionResult
    [<HttpPost("join/{id}")>]
    member __.JoinVitualContest(id:int) =
        let userInfo = getUserInfoFromControllerBase(__:>ControllerBase)
        let ctx = getDataContext()
        let vcontestIsNotExist = 
            query{
                for vcontest in ctx.ContestLog.VirtualContest do
                    where (vcontest.IdvirtualContest = id)
            }|> Seq.isEmpty
        if vcontestIsNotExist then
            let res =
                {
                    code =404s
                    result = "No such virtual contest found."
                    url = null
                }
            base.NotFound(res):>ActionResult
        else 
            let notInContest = 
                query{
                    for joiner in ctx.ContestLog.VirtualContestParticipants do
                        where (joiner.VirtualContestIdvirtualContest=id && joiner.UserIduser = userInfo.dbId)
                }|>Seq.isEmpty
            if notInContest then
                try 
                    let transactionopt = TransactionOptions(Timeout=TimeSpan.Zero,IsolationLevel=IsolationLevel.RepeatableRead)
                    use transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                                                           transactionopt,
                                                           TransactionScopeAsyncFlowOption.Enabled)
                    let elm = ctx.ContestLog.VirtualContestParticipants.``Create(user_iduser, virtual_contest_idvirtual_contest)``(userInfo.dbId,id)
                    ctx.SubmitUpdates()
                    transaction.Complete()
                    __.Ok():>ActionResult
                with
                | :? Exception as e ->
                    eprintfn "Transaction Error: Can't Create User \n      %s" e.Message
                    GC.Collect()
                    let res =
                        {
                            code =500s
                            result = "Internal Server Error"
                            url = null
                        }
                    __.StatusCode(500,res):>ActionResult
            else
                let res =
                    {
                        code =500s
                        result = "Already joined"
                        url = null
                    }
                __.StatusCode(500,res):>ActionResult

        