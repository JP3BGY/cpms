namespace ContestServer.Controllers

open System
open System.Transactions
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Authorization
open Microsoft.Extensions.Logging
open ContestServer.Setting
open ContestServer.VirtualContest
open ContestServer.Database2Data.UserInfo
open ContestServer.Types
open FSharp.Data
[<Route("api/[controller]")>]
[<ApiController>]
[<Authorize(Policy = "UserOnly")>]
type UserController (logger : ILogger<UserController>) =
    inherit ControllerBase()
    [<HttpPost("addcontestuser")>]
    member __.AddContestUser([<FromBody>] (contestUser:UserContestId)) =
        let userInfo = getUserInfoFromControllerBase (__:>ControllerBase)
        let res=setContestUser userInfo (contestUser.contestServerName) (contestUser.id)
        match res  with
        | Ok _ -> __.Ok():>ActionResult
        | Error err ->
            let res =
                {
                    code =500s
                    result = err
                    url = null
                }
            __.StatusCode(500,res):>ActionResult
    [<HttpPost("delcontestuser")>]
    member __.DelContestUser([<FromBody>] (contestUser:UserContestId)) =
        let userInfo = getUserInfoFromControllerBase (__:>ControllerBase)
        let res=delContestUser userInfo (contestUser.contestServerName) (contestUser.id)
        match res  with
        | Ok _ -> __.Ok():>ActionResult
        | Error err ->
            let res =
                {
                    code =500s
                    result = err
                    url = null
                }
            __.StatusCode(500,res):>ActionResult
    [<HttpGet("getmyinfo")>]
    member __.GetMyInfo()=
        let userInfo = getUserInfoFromControllerBase (__:>ControllerBase)
        __.Ok(userInfo)