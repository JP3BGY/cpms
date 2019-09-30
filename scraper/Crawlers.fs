module Scraper.Crawlers
open System
open Scraper.Codeforces
open Scraper.Setting
open Scraper.AtCoder
let initArray=[|
    ("Codeforces","https://codeforces.com")
|]
let crawlerArray=[|
    codeforces;atcoder
|]
let rec internalCrawlerLoop ()=
    crawlerArray|>Array.map(fun x-> x())|>Async.Parallel|>Async.RunSynchronously|>ignore

let crawlerLoop =
    async{
        internalCrawlerLoop()
    }