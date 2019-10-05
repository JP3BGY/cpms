import React from 'react';
import { BrowserRouter as Router, Route, Link } from "react-router-dom";
import Home from './pages/Home'
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
            <Link to="/account/login">Login</Link>
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
}
class App extends React.Component<{},{selected:Map<number,{url:string,name:string}>,vinfo:VirtualContestInfo}>{
  constructor(props:{}){
    super(props);
    this.state={
      selected:new Map<number,{url:string,name:string}>(),
      vinfo:{
        isSelectedVisible: true,
        date: "",
        time: "",
        duration: 3600,
      },
    };
    this.onVinfoChange=this.onVinfoChange.bind(this);
    this.addProblem=this.addProblem.bind(this);
    this.delProblem=this.delProblem.bind(this);
    this.createVContest=this.createVContest.bind(this);
  }
  componentDidMount(){
    const storage = localStorage.getItem(selectedProblemsKey);
    if(storage){
      this.setState((s)=>({selected:new Map<number,{url:string,name:string}>(JSON.parse(storage))}));
    }
    window.addEventListener('beforeunload', (event) => {
      localStorage.setItem(selectedProblemsKey,JSON.stringify([...this.state.selected]));
      event.returnValue = '';
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
  addProblem(p:Api.Problem){
    this.setState((s)=>({
      selected: s.selected.set(p.dbId,{url:p.url,name:p.name}),
    }));
  }
  delProblem(p:Api.Problem){
    this.setState((s)=>{
      let st=s.selected;
      st.delete(p.dbId);
      return{
        selected:st,
      }
    });
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
        startTime: startTime,
      }
      Api.apiCall(1,"/api/virtualcontest/create",vcontest).catch((e)=>{console.log(e);});
      console.log(vcontest);
    }
  }
  render (){
    let selectedProblems = []
    for(let item of this.state.selected){
      selectedProblems.push((
          <li><a href={item[1].url}>{item[1].name}</a></li>
      ));
    }
    let fixedSelected = (
      <footer>
        <input type="checkbox" name="isSelectedVisible" checked={this.state.vinfo.isSelectedVisible} onChange={(e)=>{this.onVinfoChange(e.target.name,e.target.checked)}}/>
        <ul>
          {selectedProblems}
        </ul>
        <input type="date" name="date" value={this.state.vinfo.date} onChange={(e)=>{this.onVinfoChange(e.target.name,e.target.value)}}/>
        <input type="time" name="time" value={this.state.vinfo.time}onChange={(e)=>{this.onVinfoChange(e.target.name,e.target.value)}}/>
        <input type="number" name="duration" value={this.state.vinfo.duration}onChange={(e)=>{this.onVinfoChange(e.target.name,e.target.valueAsNumber)}}/>
        <button onClick={this.createVContest}>Create Virtual Contest</button>
      </footer>
    );
    return (
    <Router>
      <Header></Header>
      <Route path="/" exact component={Home} />
      <Route path="/problem/" render={(props)=><Problem {...props} ps={{selected:this.state.selected,add:this.addProblem,del:this.delProblem}} />} />
      <Route path="/contest/" component={Contest} />
      <Route path="/vcontest/" component={VContest} />
      {fixedSelected}
    </Router>
    );
  }
}

export default App;
