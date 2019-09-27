module Scraper.UserCrawlers
open Scraper.Setting
open System.Threading
open System
open Scraper.Codeforces
let userCrawlerArray=
    [|
        userCodeforces;
    |]
let rec internalUserCrawlerLoop () =
    userCrawlerArray|>Array.map(fun x-> x())|>Async.Parallel|>Async.RunSynchronously|>ignore
let userCrawlerLoop =
    async{
        internalUserCrawlerLoop()
    }