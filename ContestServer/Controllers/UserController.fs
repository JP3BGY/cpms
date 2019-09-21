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
open FSharp.Data
[<Route("api/[controller]")>]
[<ApiController>]
[<Authorize(Policy = "UserOnly")>]
type UserController () =
    inherit ControllerBase()
    [<HttpPost("addcontestuser")>]
    member this.AddContestUser([<FromBody>] (contestUser:SetContestUser)) =
        let userInfo = getUserInfoFromControllerBase (this:>ControllerBase)
        let res=setContestUser userInfo (contestUser.contestServer) (contestUser.contestUserId)
        match res  with
        | Ok _ -> this.Ok():>ActionResult
        | Error err ->
            let res =
                {
                    code =500s
                    result = err
                    url = null
                }
            this.StatusCode(500,res):>ActionResult