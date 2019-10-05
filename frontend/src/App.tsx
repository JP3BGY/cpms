import React from 'react';
import { BrowserRouter as Router, Route, Link } from "react-router-dom";
import Home from './pages/Home'
import MySetting from './pages/MySetting'
import Contest from './pages/Contest'
import Problem from './pages/Problem'
import VContest from './pages/VContest'
import * as Api from './api/ApiCall'
const Header:React.FC = () =>{
  return (
    <header>
      <nav>
        <ul>
          <li>
            <Link to="/">Home</Link>
          </li>
          <li>
            <Link to="/problem">ProblemList</Link>
          </li>
          <li>
            <Link to="/contest">ContestList</Link>
          </li>
          <li>
            <Link to="/vcontest">VirtualContest</Link>
          </li>
          <li>
            <Link to="/mysetting">Account Setting</Link>
          </li>
        </ul>
      </nav>
    </header>
  )
}
const selectedProblemsKey="selectedProblemsStr";
interface VirtualContestInfo {
  isSelectedVisible:boolean,
  date:string,
  time:string,
  duration:number,
  name:string,
}
class App extends React.Component<{},{modifyId:number|null,selected:Map<number,{url:string,name:string}>,vinfo:VirtualContestInfo}>{
  constructor(props:{}){
    super(props);
    this.state={
      modifyId:null,
      selected:new Map<number,{url:string,name:string}>(),
      vinfo:{
        isSelectedVisible: true,
        date: "",
        time: "",
        duration: 3600,
        name: "",
      },
    };
    this.onVinfoChange=this.onVinfoChange.bind(this);
    this.addProblem=this.addProblem.bind(this);
    this.delProblem=this.delProblem.bind(this);
    this.createVContest=this.createVContest.bind(this);
    this.modifyVContest=this.modifyVContest.bind(this);
  }
  componentDidMount(){
    const storage = localStorage.getItem(selectedProblemsKey);
    if(storage){
      this.setState((s)=>({selected:new Map<number,{url:string,name:string}>(JSON.parse(storage))}));
    }
    window.addEventListener('beforeunload', (event) => {
      localStorage.setItem(selectedProblemsKey,JSON.stringify([...this.state.selected]));
    });
  }
  componentWillUnmount(){
    localStorage.setItem(selectedProblemsKey,JSON.stringify([...this.state.selected]));
  }
  onVinfoChange(name:string,value:any){
    this.setState((s) => {
      console.log(s);
      console.log(value);
      console.log(name);
      return {
        vinfo: {
          isSelectedVisible: (name === "isSelectedVisible" ? value: s.vinfo.isSelectedVisible),
          date: (name === "date" ? value: s.vinfo.date),
          time: (name === "time" ? value: s.vinfo.time),
          duration: (name === "duration" ? value: s.vinfo.duration),
          name: (name==="name"?value:s.vinfo.name)
        }
      }
    });
  }
  modifyVContest(id:number,vcon:Api.VContestDetails){
    let pmap=new Map<number,{name:string,url:string}>()
    vcon.problems.forEach(element => {
      pmap.set(element.dbId,{
        url:element.url,
        name:element.name,
      });
    });
    this.setState((s)=>({
      modifyId: vcon.vContest.dbId,
      selected: pmap,
      vinfo:{
        date: (new Date(1000*vcon.vContest.startTime)).toISOString().split('T')[0],
        isSelectedVisible: true,
        duration: (vcon.vContest.endTime-vcon.vContest.startTime),
        name: (vcon.vContest.name),
        time: (new Date(1000*vcon.vContest.startTime)).toTimeString().split(' ')[0],
      }
    }));
  }
  addProblem(p:Api.Problem){
    this.setState((s)=>({
      selected: s.selected.set(p.dbId,{url:p.url,name:p.name}),
    }));
  }
  delProblem(p:Api.Problem|[number,{url:string,name:string}]){
    if('dbId' in p){
      this.setState((s)=>{
        let st=s.selected;
        st.delete(p.dbId);
        return{
          selected:st,
        }
      });
    }else{
      this.setState((s)=>{
        let st=s.selected;
        st.delete(p[0]);
        return{
          selected:st,
        }
      });
    }
  }
  createVContest(){
    const timestr=this.state.vinfo.date+" "+this.state.vinfo.time;
    let startTime = Date.parse(timestr);
    if(this.state.selected.size>0&&this.state.vinfo.duration && this.state.vinfo.date!=="" && this.state.vinfo.time!=="" && startTime){
      console.log(startTime);
      let problems:number[] = Array.from(this.state.selected.keys());
      let vcontest:Api.CreateVirtualContest = {
        duration: this.state.vinfo.duration,
        problems: problems,
        startTime: startTime/1000,
        name: this.state.vinfo.name,
      }
      console.log(vcontest);
      let url = ""
      if(this.state.modifyId){
        url="/api/virtualcontest/modify/"+this.state.modifyId.toString()
      }else{
        url="/api/virtualcontest/create"
      }
      Api.apiCall(1,url,vcontest)
        .then((e)=>{
          this.setState((s)=>({
            selected:new Map<number,{url:string,name:string}>(),
            modifyId:null,
            vinfo:{
              isSelectedVisible: true,
              date: "",
              time: "",
              duration: 3600,
              name: "",
            },
          }))
        })
        .catch((e)=>{console.log(e);});
    }
  }
  render (){
    let selectedProblems = []
    for(let item of this.state.selected){
      selectedProblems.push((
        <tr>
          <td>
            <a href={item[1].url}>{item[1].name}</a>
          </td>
          <td>
            <button onClick={(e)=>this.delProblem(item)}>Delete</button>
          </td>
        </tr>
      ));
    }
    let fixedSelected = (
      <footer>
        <input type="checkbox" name="isSelectedVisible" checked={this.state.vinfo.isSelectedVisible} onChange={(e)=>{this.onVinfoChange(e.target.name,e.target.checked)}}/>
        <table>
          {selectedProblems}
        </table>
        <input type="date" name="date" value={this.state.vinfo.date} onChange={(e)=>{this.onVinfoChange(e.target.name,e.target.value)}}/>
        <input type="time" name="time" value={this.state.vinfo.time}onChange={(e)=>{this.onVinfoChange(e.target.name,e.target.value)}}/>
        <input type="number" name="duration" value={this.state.vinfo.duration}onChange={(e)=>{this.onVinfoChange(e.target.name,e.target.valueAsNumber)}}/>
        <input type="text" name="name" value={this.state.vinfo.name}onChange={(e)=>{this.onVinfoChange(e.target.name,e.target.value)}}/>
        <button onClick={this.createVContest}>{this.state.modifyId?"Modify Virtual Contest":"Create Virtual Contest"}</button>
      </footer>
    );
    return (
    <Router>
      <Header></Header>
      <Route path="/" exact component={Home} />
      <Route path="/problem/" render={(props)=><Problem {...props} ps={{selected:this.state.selected,add:this.addProblem,del:this.delProblem}} />} />
      <Route path="/contest/" component={Contest} />
      <Route path="/vcontest/" render={(props)=><VContest {...props} modifyId={this.modifyVContest}/>} />
      <Route path="/mysetting/" component={MySetting} />
      {fixedSelected}
    </Router>
    );
  }
}

export default App;
