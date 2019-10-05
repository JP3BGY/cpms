namespace ContestServer.Types
open System
[<CLIMutable>]
type Contest =
    {
        dbId:int
        name:string
        startUnixTime:int64
        url:string
        contestServer:string
    }
[<CLIMutable>]
type Submission = 
    {
        problemId:int
        userId:int
        submissionTime:int64
        submissionStatus:string
        url:string
    }
[<CLIMutable>]
type Problem = 
    {
        dbId:int
        name:string
        tags:string []
        url:string
        serverName:string
        difficulty:Nullable<Double>
        contestDbId:int
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
        dbId:int
        userName:string
        userContestIds:UserContestId[]
    }
[<CLIMutable>]
type ErrorStatus =
    {
        code:int16
        result:string
        url:string
    }
[<CLIMutable>]
type ProblemResponse =
    {
        problems:Problem[]
        elmNum:int
    }
[<CLIMutable>]
type VContest =
    {
        dbId:int
        name:string
        startTime:int64
        endTime:int64
        participants:UserInfo []
        creator:UserInfo
    }
[<CLIMutable>]
type VContestDetails = 
    {
        vContest: VContest
        problems:Problem[]
        submissions:Submission[]
        isCreator:bool
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
        duration:int64
        name:string
    }