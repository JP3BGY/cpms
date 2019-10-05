namespace ContestServer.Types
open System
[<CLIMutable>]
type Contest =
    {
        dbId:Int32
        name:string
        startUnixTime:Int64
        url:string
        contestServer:string
    }
[<CLIMutable>]
type Submission = 
    {
        problemId:Int32
        userId:Int32
        submissionTime:Int64
        submissionStatus:string
        url:string
    }
[<CLIMutable>]
type Problem = 
    {
        dbId:Int32
        name:string
        tags:string []
        url:string
        serverName:string
        difficulty:Nullable<Double>
        contestDbId:Int32
    }
[<CLIMutable>]
type UserContestId =
    {
        id:string
        contestServerName:string
    }
[<CLIMutable>]
type UserInfo = 
    {
        dbId:Int32
        userName:string
        userContestIds:UserContestId[]
    }
[<CLIMutable>]
type ErrorStatus =
    {
        code:Int16
        result:string
        url:string
    }
[<CLIMutable>]
    {
        problems:Problem[]
        submissions:Submission[]
[<CLIMutable>]
    }
[<CLIMutable>]
    {
        problems:Problem[]
        elmNum:int
    }

//Input Type

[<CLIMutable>]
type SetContestUser = 
    {
        contestServer:string
        contestUserId:string
    }
[<CLIMutable>]
type CreateVirtualContest = 
    {
        problems:int[]
        startTime: int64
        duration:Int64
    }