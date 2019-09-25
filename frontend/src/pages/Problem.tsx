import React from "react"
import * as Api from "../api/ApiCall"
class Problem extends React.Component<{},{isAwait:Boolean,err:Api.Status,problems:Api.Problem[]}>{
    constructor(props:{}){
        super(props);
        this.state={
            isAwait:false,
            err: Api.Status.Ok,
            problems: []
        }
        this.updateProblems = this.updateProblems.bind(this);
        this.updateProblems();

    }
    updateProblems(){
        if(this.state.isAwait){
            return;
        }
        this.setState(state=>
            ({
                err:state.err,
                problems:state.problems,
                isAwait:true,
            }))
        Api.apiCall(0, "/api/problem?offset=0&n=100")
            .then(res => {
                this.setState(state=>({
                    isAwait:false,
                    err:Api.Status.Ok,
                    problems: res,
                }))
            }).catch((err:Api.Status)=>{
                this.setState(state=>({
                    isAwait:false,
                    err:err,
                    problems:state.problems,
                }))
            })
    }
    render(){
        let tbody = this.state.problems.map((item,idx)=>{
            return(
                <tr>
                    <td><a href={item.url}>{item.name}</a></td>
                    <td>{item.difficulty}</td>
                    <td>{item.tags.join()}</td>
                    <td>{item.serverName}</td>
                </tr>
            )
        })
        return(
            <main>
                <table>
                    <thead><tr>
                        <td>問題名</td>
                        <td>難易度（サーバー別）</td>
                        <td>タグ</td>
                        <td>サーバー名</td>
                    </tr></thead>
                    <tbody>{tbody}</tbody>
                </table>
            </main>
        );
    }
}
export default Problem;