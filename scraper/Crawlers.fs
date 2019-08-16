module Crawlers
open System
open Codeforces
open Setting
let initArray=[|
    ("Codeforces","https://codeforces.com")
|]
let crawlerArray=[|
    codeforces
|]
let rec internalCrawlerLoop ()=
    crawlerArray|>Array.map(fun x-> x())|>Async.Parallel|>Async.RunSynchronously|>ignore

let crawlerLoop =
    async{
        internalCrawlerLoop()
    }