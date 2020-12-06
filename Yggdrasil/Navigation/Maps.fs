module Yggdrasil.Navigation.Maps

open System
open System.IO
open NLog
open Yggdrasil.Utils
let Logger = LogManager.GetCurrentClassLogger()

[<Flags>]
type CellType =
    | NO_WALK = 0
    | WALK = 1
    | SNIPE = 2
    | WATER = 4
    | CLIFF = 8

type MapData = {
    Height: uint16
    Width: uint16
    Cells: CellType[]
}

let ReadMap (bytes: byte[]) =
    {
        Width = ToUInt16 <| bytes
        Height = ToUInt16 <| bytes.[2..]
        Cells = Array.map (fun c -> Enum.Parse(typeof<CellType>, c.ToString()) :?> CellType) bytes.[4..]
    }
    
let MapCacheParser () =
    let data = File.ReadAllBytes ("maps/prontera.fld2")
    let map = ReadMap data
    map
    