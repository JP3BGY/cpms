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
let delCacheArray=[|
    cfDeleteAllCache
|]

let rec crawlerLoop ()=
    Threading.Thread.Sleep(TimeSpan.FromHours(4.0))
    crawlerArray|>Array.map(fun x-> x())|>Async.Parallel|>Async.RunSynchronously|>ignore
    delCacheArray|>Array.map(fun x->x())|>ignore
    if Console.KeyAvailable then 
        match Console.ReadKey().Key with
        | ConsoleKey.Q -> ()
        | _ -> crawlerLoop()
        else crawlerLoop()