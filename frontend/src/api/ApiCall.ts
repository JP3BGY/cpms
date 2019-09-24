export interface Contest 
{
        dbId:number
        name:string
        startUnixTime:number
        url:string
        contestServer:string
    }
export interface Submission 
    {
        problemId:number
        userId:number
        submissionTime:number
        submissionStatus:string
        url:string
    }
export interface Problem 
    {
        dbId:number
        name:string
        tags:string []
        url:string
        serverName:string
        difficulty?:number
        contestDbId:number
    }
export interface UserContestId 
    {
        id:string
        contestServerName:string
    }
export interface UserInfo 
    {
        dbId:number
        userName:string
        userContestIds:UserContestId[]
    }
export interface ErrorStatus 
    {
        code:number
        result:string
        url:string
    }
export interface ProblemAndSubmission 
    {
        problems:Problem[]
        submissions:Submission[]
    }


//Input Type

export interface SetContestUser 
    {
        contestServer:string
        contestUserId:string
    }
export interface CreateVirtualContest 
    {
        problems:number[]
        duration:number
    }
export enum Status {
    Ok,
    Error,
    Unauthorized,
    NotPermitted,
}
export let apiCall = function (idx:number,url:string,body?:any){
    const METHODS=["GET","POST"];
    return fetch(url,{
        method:METHODS[idx],
        body:JSON.stringify(body),
        headers:{
            "X-Requested-With":"XMLHttpRequest",
            "Content-Type":"application/json; charset=utf-8"
        },
        redirect:"error",
        referrer:"no-referrer",
        mode:"same-origin",
        credentials:"same-origin",
        cache:"no-store",
    }).then((res)=>{
        if(res.status===401){
            throw Status.Unauthorized;
        }else if(res.status===403){
            throw Status.NotPermitted;
        }else if(res.status>=200&&res.status<300){
            return res.json();
        }else{
            throw Status.Error;
        }
    })
}