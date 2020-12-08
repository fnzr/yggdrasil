﻿open System
open System.Diagnostics
open System.Net
open NLog
open Yggdrasil
open Yggdrasil.Navigation

let Logger = LogManager.GetCurrentClassLogger()

let BehaviorFactory id = ()

[<EntryPoint>]
let main argv =
    //let map = Yggdrasil.Navigation.Maps.MapCacheParser()    
    //let sw = Stopwatch()
    //sw.Start()
    //Pathfinding.AStar map (150, 90) (150, 85)
    //let p = (150, 90)
    //let i = Pathfinding.ToIndex map p
    //printfn "%A" <| Pathfinding.ToPoint map i
    //printfn "%A" sw.ElapsedMilliseconds
    //let dispatcher = Scheduling.DispatcherFactory()
    //let tick = Scheduling.GetCurrentTick()
    //dispatcher.Post <| (tick+500u, TimedEvent tick)
    //printf "Done"
    Maps.LoadMaps()
    let loginServer = IPEndPoint  (IPAddress.Parse "127.0.0.1", 6900)
    let (mailboxes, login) = API.CreateServerMailboxes loginServer BehaviorFactory
    login "roboco" "111111"
    API.CommandLineHandler mailboxes
    //Console.ReadKey() |> ignore 
    0 // return an integer exit code
