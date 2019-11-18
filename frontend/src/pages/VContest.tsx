import React from "react"
import { BrowserRouter as Router, Route, Link , match, Switch} from "react-router-dom";
import * as Api from "../api/ApiCall"
import { string, number } from "prop-types";
import { tsDeclareFunction } from "@babel/types";
import { isMainThread } from "worker_threads";
class VContestDetails extends React.Component<{match:match<{DbId:string}>,modifyId:(id:number,probs:Api.VContestDetails)=>void},{details:Api.VContestDetails|null}>{
    constructor(props:{match:match<{DbId:string}>,modifyId:(id:number,probs:Api.VContestDetails)=>void}){
        super(props);
        console.log(props.match);
        this.state={
            details:null
        }
    }
    componentDidMount(){
        const dbId = parseInt(this.props.match.params.DbId,10);
        if(dbId){
            Api.apiCall(0,"/api/virtualcontest/details/"+dbId.toString())
                .then((res)=>{
                    this.setState((s)=>({
                        details:res,
                    }));
                }).catch((e)=>{
                    this.setState((s)=>({
                        details: null,
                    }));
                })
        }
    }
    render(){
        const details = this.state.details;
        if(details!==undefined&&details&&details!==null){
            const join = ()=>{
                Api.apiCall(1,"/api/virtualcontest/join/"+details.vContest.dbId).catch((e)=>{
                    console.log("error ",e);
                });
            }
            const header = details.problems.map((item,idx)=>(
                <th><a href={item.url}>{item.name}</a></th>
            ))
            let problemMap = new Map<number,number>();
            details.problems.forEach((elm,idx) => {
                problemMap.set(elm.dbId,idx);
            });
            let dbId2User = new Map<number,Api.UserInfo>();
            let tableElm = new Map<number,Array<Api.Submission>> ();
            let modify
            if(details.isCreator){
                modify=(
                    <button onClick={(e)=>this.props.modifyId(details.vContest.dbId,details)}>Modify</button>
                )
            }else{
                modify=""
            }
            details.vContest.participants.forEach(elm=>{
                dbId2User.set(elm.dbId,elm);
                tableElm.set(elm.dbId,Array<Api.Submission>(details.problems.length));
            })
            details.submissions.forEach(elm => {
                const idx = problemMap.get(elm.problemId);
                if(idx!==undefined){
                    let cur=tableElm.get(elm.userId);
                    if(cur===undefined){
                        cur=[];
                        cur.length=details.problems.length;
                    }
                    if(cur!==undefined&&(!cur[idx]||(cur[idx].submissionTime<elm.submissionTime))){
                        if(elm.submissionTime<details.vContest.endTime){
                            cur[idx]=elm;
                        }else if((!cur[idx]||cur[idx].submissionStatus!=="AC")&&elm.submissionStatus==="AC"){
                            cur[idx]=elm;
                        }
                    }
                }
            });
            let body=[...tableElm].map((item)=>{
                let probs = item[1].map((item,idx)=>{
                    if(item === undefined){
                        return (<td></td>);
                    }
                    return (
                    <td><a href={item.url}>{item.submissionStatus==="AC"&&item.submissionTime<details.vContest.startTime?"Already ACed":item.submissionStatus==="AC"&&item.submissionTime>details.vContest.endTime?"ACed after contest":item.submissionStatus}</a></td>
                );})
                for (let index = 0; index < probs.length;index++) {
                    if(probs[index]===undefined){
                        probs[index]=(<td></td>);
                    }
                }
                const userInfo = dbId2User.get(item[0]) as Api.UserInfo;
                return (
                    <tr>
                        <th>{userInfo.userName}</th>
                        {probs}
                    </tr>
                );
            })
            return (
                <main>
                    <h2>{details.vContest.name}</h2>
                    <span>{new Date(details.vContest.startTime*1000).toString()}~{new Date(details.vContest.endTime*1000).toString()}</span>
                    <button onClick={join}>Join Now</button>
                    {modify}
                    <table>
                        <thead>
                            <th>User</th>
                            {header}
                        </thead>
                        <tbody>
                            {body}
                        </tbody>
                    </table>
                </main>
            );
        }else{
            return (
                <main>
                </main>
            );
        }
    }
}
class VContest extends React.Component<{match:match,modifyId:(id:number,probs:Api.VContestDetails)=>void},{vcontests:Api.VContest[]}>{
    constructor(props:{match:match,modifyId:(id:number,probs:Api.VContestDetails)=>void}){
        super(props);
        this.state ={
            vcontests:[],
        };
    }
    componentDidMount(){
        Api.apiCall(0,"/api/virtualcontest").then((e:Api.VContest[])=>{
            this.setState((s)=>({
                vcontests:e
            }))
        }).catch((e)=>{
            console.log(e);
        })
    }
    render(){
        const vcontests = 
            this.state.vcontests.map((item,idx)=>(
                <tr>
                    <td><Link to={`${this.props.match.url}/${item.dbId.toString()}`}>{item.name}</Link></td>
                    <td>{new Date(item.startTime*1000).toString()}</td>
                    <td>{new Date(item.endTime*1000).toString()}</td>
                </tr>
            ))
        const vcontesttable =(
            <table>
                <thead>
                    <tr>
                        <td>コンテスト名</td>
                        <td>開始時間</td>
                        <td>終了時間</td>
                    </tr>
                </thead>
                <tbody>
                    {vcontests}
                </tbody>
            </table>
        );
        return(
            <main>
                <h1>Virtual Contest</h1>
                <Switch>
                    <Route path={this.props.match.url} exact render={(props)=>(vcontesttable)} />
                    <Route path={`${this.props.match.url}/:DbId`} render={(props)=><VContestDetails {...props} modifyId={this.props.modifyId}/>} />
                </Switch>
            </main>
        );
    }
}
export default VContest;