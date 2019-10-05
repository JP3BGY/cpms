import React from "react"
import { BrowserRouter as Router, Route, match, Link } from "react-router-dom";
import * as Api from "../api/ApiCall"
class UserInfo extends React.Component<{match:match},{}>{
    constructor(props:{match:match}){
        super(props);
    }
    render(){
        return (
            <main>

            </main>
        );
    }
}