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
    Threading.Thread.Sleep(500)
    codeforces()
    delCacheArray|>Array.map(fun x->x())|>ignore
    if Console.KeyAvailable then 
        match Console.ReadKey().Key with
        | ConsoleKey.Q -> ()
        | _ -> crawlerLoop()
        else crawlerLoop()