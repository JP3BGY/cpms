import React from "react"
import { BrowserRouter as Router, Route, match, Link } from "react-router-dom";
import * as Api from "../api/ApiCall"
class MySetting extends React.Component<{},{myInfo:Api.UserInfo|null,addInfo:Api.UserContestId}>{
    constructor(props:{}){
        super(props);
        this.state={
            myInfo:null,
            addInfo:{
                id:"",
                contestServerName:"AtCoder",
            }
        }
        this.settingUser = this.settingUser.bind(this);
        this.handleAddInfoChange = this.handleAddInfoChange.bind(this);
    }
    componentDidMount(){
        Api.apiCall(0,"/api/user/getmyinfo").then((res)=>{
            this.setState((s)=>({
                myInfo:res
            }));
        }).catch((e)=>{
            console.log(e);
            this.setState((s)=>({
                myInfo:e
            }));
        })
    }
    settingUser(name:string,userInfo:Api.UserContestId){
        Api.apiCall(1,"/api/user/"+name+"contestuser",userInfo).then((x)=>{
            Api.apiCall(0,"/api/user/getmyinfo").then((res)=>{
                this.setState((s)=>({
                    myInfo:res
                }));
            }).catch((e)=>{
                console.log(e);
                this.setState((s)=>({
                    myInfo:e
                }));
            });
        }).catch(
            (e)=>{
                console.log(e);
            }
        )
    }
    handleAddInfoChange(name:string,value:string){
        this.setState((s)=>({
            addInfo:{
                contestServerName: name==="csn"?value:s.addInfo.contestServerName,
                id:name==="id"?value:s.addInfo.id,
            }
        }))
    }
    render(){
        if (this.state.myInfo){
            const users = this.state.myInfo.userContestIds.map((item)=>(
                <tr>
                    <td>{item.id}</td>
                    <td>{item.contestServerName}</td>
                    <td><button onClick={(e)=>this.settingUser("del",item)}>Delete</button></td>
                </tr>
            ))
            return (
                <main>
                    <h1>Setting</h1>
                    <h2>Your Contest Server Account</h2>
                    <table>
                        {users}
                    </table>
                    <h2>Add Contest Server Account</h2>
                    <select value={this.state.addInfo.contestServerName} onChange={(e)=>this.handleAddInfoChange("csn", e.target.value)}>
                        <option selected value="AtCoder">AtCoder</option>
                        <option value="Codeforces">Codeforces</option>
                    </select>
                    <input type="text" value={this.state.addInfo.id} onChange={(e)=>this.handleAddInfoChange("id",e.target.value)}/>
                    <button onClick={(e)=>this.settingUser("add",this.state.addInfo)}>Add Contest Server Account</button>
                </main>
            );
        }
        return (
            <main>
                <h1>Setting</h1>
                <h2>Your Contest Server Account</h2>
                <h2>Add Contest Server Account</h2>
            </main>
        );
    }
}
export default MySetting