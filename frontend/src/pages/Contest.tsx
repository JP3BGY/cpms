import React from "react"
import * as Api from "../api/ApiCall"
class Contest extends React.Component<{},{isAwait:Boolean,err:Api.Status,contests:Api.Contest[]}>{
    constructor(props:{}){
        super(props);
        this.state={
            isAwait:false,
            err: Api.Status.Ok,
            contests: []
        }
        this.updateContest = this.updateContest.bind(this);
        this.updateContest();

    }
    updateContest(){
        if(this.state.isAwait){
            return;
        }
        this.setState(state=>
            ({
                err:state.err,
                contests:state.contests,
                isAwait:true,
            }))
        Api.apiCall(0, "/api/contest")
            .then(res => {
                this.setState(state=>({
                    isAwait:false,
                    err:Api.Status.Ok,
                    contests: res,
                }))
            }).catch((err:Api.Status)=>{
                this.setState(state=>({
                    isAwait:false,
                    err:err,
                    contests:state.contests,
                }))
            })
    }
    render(){
        let tbody = this.state.contests.map((item,idx)=>{
            return(
                <tr key={item.dbId}>
                    <td><a href={item.url}>{item.name}</a></td>
                    <td>{new Date(item.startUnixTime*1000).toLocaleString("ja-JP")}</td>
                    <td>{item.contestServer}</td>
                </tr>
            )
        })
        return(
            <main>
                <table>
                    <thead><tr>
                        <th>コンテスト名</th>
                        <th>開始時間</th>
                        <th>コンテストサーバー</th>
                    </tr></thead>
                    <tbody>{tbody}</tbody>
                </table>
            </main> 
        );
    }
}
export default Contest;