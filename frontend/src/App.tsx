import React from 'react';
import { BrowserRouter as Router, Route, Link } from "react-router-dom";
import Home from './pages/Home'
import Contest from './pages/Contest'
import Problem from './pages/Problem'
import VContest from './pages/VContest'
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
const App: React.FC = () => {
  return (
    <Router>
      <Header></Header>
      <Route path="/" exact component={Home} />
      <Route path="/problem/" component={Problem} />
      <Route path="/contest/" component={Contest} />
      <Route path="/vcontest/" component={VContest} />
    </Router>
  );
}

export default App;
