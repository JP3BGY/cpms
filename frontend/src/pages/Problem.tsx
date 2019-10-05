import React, { InputHTMLAttributes } from "react"
import * as Api from "../api/ApiCall"
import * as RouterDom from "react-router-dom"
import * as Router from "react-router"
import * as History from "history"
enum Error {
    Ok = 0,
    InputValue,
    NetworkError,
}
interface ProblemState {
    page:number,
    num:number,
    maxNum:number,
    isTagVisible:boolean,
    isAwait:boolean,
    problems:Api.Problem[],
    err:Error,
    search:string,
}
interface SelectedProblemProps{
    selected:Map<number,{url:string,name:string}>,
    add:(p:Api.Problem)=>void,
    del:(p:Api.Problem)=>void,
}
class Problem extends React.Component<{location: History.Location ,history:History.History, ps:SelectedProblemProps},ProblemState>{
    private pageInput=React.createRef<HTMLInputElement>()
    private numInput=React.createRef<HTMLInputElement>()
    constructor(props:{location:History.Location,history:History.History, ps:SelectedProblemProps}){
        super(props);
        let params = new URLSearchParams(props.location.search)
        let page,num;
        const pageParam=params.get("page");
        if(pageParam){
            page=parseInt(pageParam);
        }
        const numParam=params.get("num");
        if(numParam){
            num=parseInt(numParam);
        }
        this.state={
            page:page?page:0,
            num:num?num:50,
            maxNum:0,
            isTagVisible:false,
            err:Error.Ok,
            isAwait:false,
            problems:[],
            search:"",
        };
        this.changePage = this.changePage.bind(this);
        this.changeNum = this.changeNum.bind(this);
        this.changeMaxNum = this.changeMaxNum.bind(this);
        this.updateProblems = this.updateProblems.bind(this);
        this.changeTagVisible = this.changeTagVisible.bind(this);
    }
    componentDidMount(){
        this.updateProblems();
    }
    componentDidUpdate(prevProps:any,prevState:ProblemState){
        if(prevState.num!==this.state.num||prevState.page!==this.state.page){
            this.updateProblems();
        }
    }
    updateProblems(){
        if(this.state.isAwait){
            return;
        }
        this.setState(state=>
            ({
                err:state.err,
                isAwait:true,
            }))
        Api.apiCall(0, "/api/problem?offset="+(this.state.page*this.state.num).toString()+"&n="+this.state.num.toString())
            .then(res => {
                this.changeMaxNum(res.elmNum);
                this.setState(state=>({
                    isAwait:false,
                    err:Error.Ok,
                    problems:res.problems,
                }))
            }).catch((err:Api.Status)=>{
                this.setState(state=>({
                    isAwait:false,
                    err:Error.NetworkError,
                    problems:state.problems,
                }))
            })
    }
    isNumber(x:number){
        if(isNaN(x)||x<0){
            this.setState(s=>({
                err: Error.InputValue,
            }));
            return false;
        }
        return true;
    }
    changePage(page:number){
        if(this.isNumber(page)){
            console.log("changePage",page);
            this.props.history.push({search:"?page="+page.toString()+"&num="+this.state.num.toString()+"&query="});
            this.setState(s=>({
                page:page,
            }));
            const probPage = (document.getElementById("problemPage")) as HTMLInputElement;
            if(probPage){
                probPage.value = page.toString()
            }
        }
    }
    changeNum(num:number){
        console.log("changeNum",num);
        if(this.isNumber(num)){
            this.props.history.push({search:"?page="+this.state.page.toString()+"&num="+num.toString()});
            this.setState(s=>({
                num: num,
            }));
        }
    }
    changeMaxNum(maxNum:number){
        console.log("changeMaxNum",maxNum);
        this.setState(s=>({
            maxNum:maxNum,
        }));
    }
    changeTagVisible(e:boolean){
        this.setState((s)=>({isTagVisible:e}));
    }
    render(){
        const selector=(
            <div className="pageSelector">
                <button className="selectorLeft" onClick={(e)=>this.changePage((this.state.page)-1)} >prev</button>
                <input type="number"  ref={this.pageInput} 
                    defaultValue={this.state.page.toString()} 
                    onKeyUp={(e)=>{if(this.pageInput.current&&e.key==="Enter"){this.changePage(this.pageInput.current.valueAsNumber)}}} 
                    onBlur={(e)=>this.changePage(parseInt(e.target.value,10))} 
                    id="problemPage"
                    className="problemInput" />
                <span>/{Math.ceil (this.state.maxNum/this.state.num)-1}</span>
                <input type="number" ref={this.numInput} 
                    defaultValue={this.state.num.toString()} 
                    onKeyUp={(e)=>{if(this.numInput.current&&e.key==="Enter"){this.changeNum(this.numInput.current.valueAsNumber)}}} 
                    onBlur={(e)=>this.changeNum(parseInt(e.target.value,10))} 
                    className="problemInput" />
                <button className="selectorRight" onClick={(e)=>this.changePage((this.state.page)+1)} >next</button>
            </div>
        );
        let tbody = this.state.problems.map((item,idx)=>{
            return(
                <tr key={item.dbId}>
                    <td><input type="checkbox" checked={this.props.ps.selected.has(item.dbId)} onChange={(e)=>{if(e.target.checked){this.props.ps.add(item);}else{this.props.ps.del(item);}}}/></td>
                    <td><a href={item.url}>{item.name}</a></td>
                    <td>{item.difficulty}</td>
                    {this.state.isTagVisible && <td>{item.tags.join()}</td>}
                    <td>{item.serverName}</td>
                </tr>
            )
        })
        const table=(
            <div>
                <div>
                </div>
                <table>
                    <thead><tr>
                        <th>選択</th>
                        <th>問題名</th>
                        <th>難易度（サーバー別）</th>
                        {this.state.isTagVisible && <th>タグ</th>}
                        <th>サーバー名</th>
                    </tr></thead>
                    <tbody>{tbody}</tbody>
                </table>
            </div>
        );
        const search = (
            <div className="search">
                <input type="checkbox" checked={this.state.isTagVisible} onChange={(e)=>{this.changeTagVisible(e.target.checked);}}/>
                <input type="text" value={this.state.search}/>
            </div>
        );
        return(
            <main>
                {selector}
                {search}
                {table}
            </main>
        );
    }
}
export default Problem;