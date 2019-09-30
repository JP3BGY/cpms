import React, { InputHTMLAttributes } from "react"
import * as Api from "../api/ApiCall"
import * as RouterDom from "react-router-dom"
import * as Router from "react-router"
import * as History from "history"
interface tableProps {page:number,num:number,maxNum:number}
interface pageProps {tableP:tableProps,changePage:(page:number)=>void,changeNum:(num:number)=>void}
enum selectorError {
    Ok = 0,
    InputValue = 1,
}
class PageSelector extends React.Component<pageProps,{err:selectorError}>{
    private pageInput=React.createRef<HTMLInputElement>()
    private numInput=React.createRef<HTMLInputElement>()
    constructor(props:Readonly<pageProps>){
        super(props);
        this.state ={
            err: selectorError.Ok,
        }
        this.onChangePage = this.onChangePage.bind(this)
        this.onChangeNum = this.onChangeNum.bind(this)
    }
    onChangeNum(value:number){
        if(isNaN(value)||value<0){
            this.setState(s=>({
                err: selectorError.InputValue,
            }));
        }else{
            this.props.changeNum(value);
        }
    }
    onChangePage(value:number){
        if(isNaN(value)||value<0){
            this.setState(s=>({
                err: selectorError.InputValue,
            }));
        }else{
            console.log(value);
            this.props.changePage(value);
            const probPage = (document.getElementById("problemPage")) as HTMLInputElement;
            if(probPage){
                probPage.value = value.toString()
            }
        }
    }
    render(){
        return (
            <div className="pageSelector">
                <button className="selectorLeft" onClick={(e)=>this.onChangePage((this.props.tableP.page)-1)} >prev</button>
                <input type="number"  ref={this.pageInput} 
                    defaultValue={this.props.tableP.page.toString()} 
                    onKeyUp={(e)=>{if(this.pageInput.current&&e.key==="Enter"){this.onChangePage(this.pageInput.current.valueAsNumber)}}} 
                    onBlur={(e)=>this.onChangePage(parseInt(e.target.value,10))} 
                    id="problemPage"
                    className="problemInput" />
                <span>/{Math.ceil (this.props.tableP.maxNum/this.props.tableP.num)-1}</span>
                <input type="number" ref={this.numInput} 
                    defaultValue={this.props.tableP.num.toString()} 
                    onKeyUp={(e)=>{if(this.numInput.current&&e.key==="Enter"){this.onChangeNum(this.numInput.current.valueAsNumber)}}} 
                    onBlur={(e)=>this.onChangeNum(parseInt(e.target.value,10))} 
                    className="problemInput" />
                <button className="selectorRight" onClick={(e)=>this.onChangePage((this.props.tableP.page)+1)} >next</button>
            </div>
        );
    }
}
class ProblemTable extends React.Component<{tableP:tableProps,changeMaxNum:(maxNum:number)=>void},{isAwait:Boolean,err:Api.Status,problems:Api.Problem[]}>{
    constructor(props:Readonly<{tableP:tableProps,changeMaxNum:(maxNum:number)=>void}>){
        super(props);
        this.state={
            isAwait:false,
            err: Api.Status.Ok,
            problems: [],
        }
        this.updateProblems = this.updateProblems.bind(this);

    }
    componentDidMount(){
        this.updateProblems();
    }
    componentDidUpdate(prevProps:{tableP:tableProps,changeMaxNum:(maxNum:number)=>void}){
        if(prevProps.tableP!==this.props.tableP){
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
                problems:state.problems,
                isAwait:true,
            }))
        Api.apiCall(0, "/api/problem?offset="+(this.props.tableP.page*this.props.tableP.num).toString()+"&n="+this.props.tableP.num.toString())
            .then(res => {
                this.props.changeMaxNum(res.elmNum);
                this.setState(state=>({
                    isAwait:false,
                    err:Api.Status.Ok,
                    problems: res.problems,
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
            <table>
                <thead><tr>
                    <td>問題名</td>
                    <td>難易度（サーバー別）</td>
                    <td>タグ</td>
                    <td>サーバー名</td>
                </tr></thead>
                <tbody>{tbody}</tbody>
            </table>
        );
    }
}
class Problem extends React.Component<{location: History.Location ,history:History.History},tableProps>{
    constructor(props:{location:History.Location,history:History.History}){
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
        };
        this.changePage = this.changePage.bind(this);
        this.changeNum = this.changeNum.bind(this);
        this.changeMaxNum = this.changeMaxNum.bind(this);
    }
    changePage(page:number){
        console.log("changePage",page);
        this.props.history.push({search:"?page="+page.toString()+"&num="+this.state.num.toString()});
        this.setState(s=>({
            page:page,
            num:s.num,
        }));
    }
    changeNum(num:number){
        console.log("changeNum",num);
        this.props.history.push({search:"?page="+this.state.page.toString()+"&num="+num.toString()});
        this.setState(s=>({
            page: s.page,
            num: num,
        }));
    }
    changeMaxNum(maxNum:number){
        console.log("changeMaxNum",maxNum);
        this.setState(s=>({
            page: s.page,
            num: s.num,
            maxNum:maxNum,
        }));
    }
    render(){
        return(
            <main>
            <PageSelector tableP={this.state} changePage={this.changePage} changeNum={this.changeNum} />
            <ProblemTable tableP={this.state} changeMaxNum={this.changeMaxNum}/>
            </main>
        );
    }
}
export default Problem;