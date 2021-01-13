﻿// Learn more about F# at http://fsharp.org

open System
open System.Collections
open System.Net
open NLog
open PacketDotNet
open SharpPcap
let Logger = LogManager.GetLogger("LivePacket")
let ServerIP = IPAddress.Parse "172.19.0.2"
let ClientIP = IPAddress.Parse "192.168.2.3"
let Agent = Yggdrasil.Agent.Agent.Agent()
let MapToClientCallback = Yggdrasil.IO.Incoming.OnPacketReceived Agent
let mutable MapToClientQueue = Array.empty
let mutable ClientToMapQueue = Array.empty

let OnClientToServerPacket (packetType: uint16) packetData =
    match packetType with
    | 0x0087us -> () //ZC_NOTIFY_PLAYERMOVE
    | 0x0360us -> () //CZ_REQUEST_TIME2
    | 0x007dus -> () //CZ_NOTIFY_ACTORINIT
    | 0x08c9us -> () //cash shop request?
    | 0x014fus -> () //CZ_REQ_GUILD_MENU
    | 0x0447us -> () //CZ_BLOCKING_PLAY_CANCEL
    | 0x0368us -> () //CZ_REQNAME2
    | 0x035fus -> () //CZ_REQUEST_MOVE2
    | _ -> Logger.Info ("Packet: {packetType:X}", packetType)

let OnPacketArrival (e: CaptureEventArgs) =
    match Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data).Extract<TcpPacket>() with
    | null -> ()
    | tcpPacket ->
        let ipPacket = tcpPacket.ParentPacket :?> IPPacket
        match ipPacket.SourceAddress with
        | ip when ServerIP = ip && ipPacket.DestinationAddress = ClientIP ->
            let q = Array.concat [|MapToClientQueue; tcpPacket.PayloadData|]
            MapToClientQueue <- Yggdrasil.IO.Stream.Reader q MapToClientCallback
        | ip when ClientIP = ip ->
            let q = Array.concat [|ClientToMapQueue; tcpPacket.PayloadData |]
            ClientToMapQueue <- Yggdrasil.IO.Stream.Reader q OnClientToServerPacket
        | _ -> ()
    ()

[<EntryPoint>]
let main argv =
    let devices = CaptureDeviceList.Instance;
    if devices.Count < 1 then invalidArg "devices" "No device found in this machine. Did you run as root?"
    //Seq.iteri (fun i (d: ICaptureDevice) -> printfn "%i) %s" i d.Name) (Seq.cast devices)
    let device = devices.[2]
    device.OnPacketArrival.Add(OnPacketArrival)
    device.Open(DeviceMode.Promiscuous, 1000)
    
    let filter = "ip and tcp and tcp port 5121";
    device.Filter <- filter;
    printfn "Listening on [%s] with filter \"%s\"" device.Name filter
    device.Capture()
    device.Close();
    0 // return an integer exit code
