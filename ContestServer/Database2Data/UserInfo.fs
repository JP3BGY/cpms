module ContestServer.Database2Data.UserInfo
open System
open System.Web
open System.Text
open ContestServer.Setting
open Microsoft.AspNetCore.Mvc
open FSharp.Data
type UserDb = 
    {
        Iduser:Int32
        UserEmail:string
        UserLogin:string
    }
type UserContestId =
    {
        id:string
        contestServerName:string
    }
type UserInfo = 
    {
        dbId:Int32
        userName:string
        userContestIds:UserContestId[]
    }
let getUserInfo user = 
    let ctx = getDataContext()
    let uCIds =
        query{
            for wuser in ctx.ContestLog.WatchingUser do
                where (wuser.UserIduser = user.Iduser)
                join contestUser in ctx.ContestLog.ContestUsers on (wuser.ContestUsersUserId=contestUser.UserId)
                join contestServer in ctx.ContestLog.ContestServer on (contestUser.ContestServerContestServerId = contestServer.ContestServerId)
                select (contestUser.ContestUserId,contestServer.ContestServerName)
        }|>Seq.map(
            fun (userid,contestname) -> 
                {
                    id = userid
                    contestServerName = contestname
                }
        )|>Seq.toArray
    {
        dbId = user.Iduser
        userName = user.UserLogin
        userContestIds = uCIds
    }
let getUserInfoFromId userId =
    let ctx=getDataContext()
    query{
        for user in ctx.ContestLog.User do  
            where (user.Iduser=userId)
            select(user)
            exactlyOne
    }|>fun x->x.MapTo<UserDb>()
    |>getUserInfo
let getUserInfoFromLogin userLogin =
    let ctx=getDataContext()
    query{
        for user in ctx.ContestLog.User do
            where(user.UserLogin = userLogin)
            select(user)
            exactlyOne
    }|>fun x->x.MapTo<UserDb>()
    |>getUserInfo
let getUserInfoFromControllerBase (baseController:ControllerBase) =
    getUserInfoFromLogin (baseController.User.FindFirst("github:login").Value)

let getUserInfos () =
    let ctx = getDataContext()
    query{
        for user in ctx.ContestLog.User do
            select (user)
    }|>Seq.map(fun x->x.MapTo<UserDb>())
    |>Seq.map(getUserInfo)
let findAndCreateContestUser serverName contestUserId =
    let ctx = getDataContext()
    match serverName with
    | "Codeforces" ->
        let exist = 
            async{
                let! res=Http.AsyncRequest("https://codeforces.com/api/user.rating?handle="+HttpUtility.UrlEncode(contestUserId,Encoding.UTF8))
                return res.StatusCode = 200
            }|>Async.RunSynchronously
        if exist then
            let server = 
                query{
                    for server in ctx.ContestLog.ContestServer do 
                    where (server.ContestServerName = serverName) 
                    exactlyOne}
            let elm=ctx.ContestLog.ContestUsers.``Create(contestUserId, contest_server_contestServerId)``(contestUserId,server.ContestServerId)
            ctx.SubmitUpdates()
            Ok(elm)
        else 
            Error()
    | _ -> Error()

        
let setContestUser userInfo serverName contestUserId =
    let ctx = getDataContext()
    let server = 
        try
            Ok(query{
                for server in ctx.ContestLog.ContestServer do
                    where (server.ContestServerName = serverName)
                    exactlyOne
            })
        with
        | :? SystemException as se ->
            Error()
    match server with
    | Ok x ->
        let contestUser = 
            try
                Ok(query{
                    for user in ctx.ContestLog.ContestUsers do
                        where (user.ContestServerContestServerId = x.ContestServerId && user.ContestUserId = contestUserId )
                        exactlyOne
                })
            with
            | :? SystemException as se ->
                Error()
        match contestUser with
        | Ok x->
            let elm=
                query{
                    for wuser in ctx.ContestLog.WatchingUser do
                        where (wuser.UserIduser = userInfo.dbId && wuser.ContestUsersUserId = x.UserId)
                }
            if Seq.isEmpty elm then
                ctx.ContestLog.WatchingUser.``Create(contest_users_userId, user_iduser)``(x.UserId,userInfo.dbId)|>ignore
                ctx.SubmitUpdates()
                Ok()
            else
                Error("Already added.")
        | Error _->
            let contestUser = findAndCreateContestUser serverName contestUserId
            match contestUser with
            | Ok x ->
                ctx.ContestLog.WatchingUser.``Create(contest_users_userId, user_iduser)``(x.UserId,userInfo.dbId)|>ignore
                ctx.SubmitUpdates()
                Ok()
            | Error _ -> Error("No such user is in the server.")
            
    | Error _->
        Error("No such contest server is found or supported.")