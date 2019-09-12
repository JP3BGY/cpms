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
    userCrawlerArray|>Array.map(fun x -> x())|>ignore
    Thread.Sleep(TimeSpan.FromMinutes(5.0))
    internalUserCrawlerLoop()
let userCrawlerLoop =
    async{
        internalUserCrawlerLoop()
    }