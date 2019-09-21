namespace ContestServer.Controllers

open System
open System.Transactions
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
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
type VirtualContestController () =
    inherit ControllerBase()
    [<HttpGet>]
    member this.GetVirtualContests() =
        let elms = getVirtualContests ()
        base.Ok(elms)
    [<HttpGet("problems/{id}")>]
    member this.GetProblems(id) =
        let res = getProblemsOfVirtualContest id (getUserInfoFromControllerBase (this:>ControllerBase))
        match res with
        | Ok (probs,subs)->
            base.Ok({
                problems=probs
                submissions=subs
            }):>ActionResult
        | Error err ->
            let res = 
                {
                    code=500s
                    result = err
                    url = null
                }
            this.StatusCode(500,res):>ActionResult

    [<HttpPost("create")>]
    member this.Post([<FromBody>] setting) =
        let duration = setting.duration
        let problemIds = setting.problems
        let userInfo = getUserInfoFromControllerBase (this:>ControllerBase)
        let result = createContestNow userInfo duration problemIds
        match result with
        | Ok x -> this.Ok(x):>ActionResult
        | Error err -> 
            let res = 
                {
                    code=500s
                    result = err
                    url = null
                }
            this.StatusCode(500,res):>ActionResult
    [<HttpPost("join/{id}")>]
    member this.JoinVitualContest(id:int) =
        let userInfo = getUserInfoFromControllerBase(this:>ControllerBase)
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
            try 
                let transactionopt = TransactionOptions(Timeout=TimeSpan.Zero,IsolationLevel=IsolationLevel.Serializable)
                use transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                                                       transactionopt,
                                                       TransactionScopeAsyncFlowOption.Enabled)
                let elm = ctx.ContestLog.VirtualContestParticipants.``Create(user_iduser, virtual_contest_idvirtual_contest)``(userInfo.dbId,id)
                ctx.SubmitUpdates()
                transaction.Complete()
                this.Ok():>ActionResult
            with
            | :? TransactionAbortedException as te ->
                eprintfn "Transaction Error: Can't Create User \n      %s" te.Message
                GC.Collect()
                let res =
                    {
                        code =500s
                        result = "Internal Server Error"
                        url = null
                    }
                this.StatusCode(500,res):>ActionResult
        