module ContestServer.Database2Data.Contest
open System
open ContestServer.Setting
type Contest =
    {
        dbId:Int32
        name:string
        startUnixTime:Int64
        url:string
        contestServer:string
    }
let contest2Url serverName serverContestId =
    match serverName with
    | "Codeforces" -> Ok ("https://codeforces.com/contest/"+serverContestId)
    | "AtCoder" -> Ok ("https://atcoder.jp/contests/"+serverContestId)
    | "TopCoder" -> Error ("Not Supported")
    | _ -> Error "Not Supported"
let getContests () =
    let ctx = getDataContext()
    let elms = 
            query{
                for contest in ctx.ContestLog.Contest do
                    sortByDescending contest.ContestStartTime
            }
    elms|>Seq.map(
        fun elm ->
            let servername = query{
                for server in ctx.ContestLog.ContestServer do
                    where (elm.ContestServerContestServerId = server.ContestServerId)
                    select server.ContestServerName
                    exactlyOne
            }
            {
                dbId = elm.ContestId
                name = elm.ContestName
                startUnixTime = elm.ContestStartTime
                contestServer = servername
                url = 
                    match (contest2Url servername elm.ContestServerContestId) with
                    |Ok x -> x
                    |Error y -> ""
            }
    )|>Seq.toArray