namespace ContestServer.Controllers

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.OAuth
open Microsoft.AspNetCore.Authorization
open ContestServer.Setting
open ContestServer.Database2Data.Contest
type SpaController () =
    inherit ControllerBase()
    [<HttpGet>]
    member this.Get()=
        base.File("~/index.html","text/html")