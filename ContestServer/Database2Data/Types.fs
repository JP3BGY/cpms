namespace ContestServer.Types
open System
type Contest =
    {
        dbId:Int32
        name:string
        startUnixTime:Int64
        url:string
        contestServer:string
    }
type Submission = 
    {
        problemId:Int32
        userId:Int32
        submissionTime:Int64
        submissionStatus:string
        url:string
    }
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
type ErrorStatus =
    {
        code:Int16
        result:string
        url:string
    }


//Input Type

type SetContestUser = 
    {
        contestServer:string
        contestUserId:string
    }
[<CLIMutable>]
type CreateVirtualContest = 
    {
        problems:int[]
        duration:Int64
    }
[<CLIMutable>]
type ProblemAndSubmission = 
    {
        problems:Problem[]
        submissions:Submission[]
    }