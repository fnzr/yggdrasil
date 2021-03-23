module Yggdrasil.World.Types

open System
open Yggdrasil.Types

type Entity = {
    Id: Id
    Type: EntityType
    Name: string
}

type Position = {
    Id: Id
    Coordinates: int16 * int16
}
let InvalidCoordinates = (-1s, -1s)

[<CustomComparison; CustomEquality>]
type TrackedEntity =
    {
        Id: Id
        Type: EntityType
        Name: string
        Coordinates: int16 * int16
    }
    interface IComparable with
        override this.CompareTo o =
            match o with
            | :? TrackedEntity -> this.Id.CompareTo((o :?> TrackedEntity).Id)
            | _ -> invalidArg (string (o.GetType())) "Invalid comparison for TrackedEntity"
    override this.Equals o =
        match o with
        | :? TrackedEntity -> (o :?> TrackedEntity).Id = this.Id
        | _ -> false
    override this.GetHashCode() = this.Id.GetHashCode()

type Health = {
    Id: Id
    HP: int
    MaxHP: int
}

type Movement = {
    Id: Id
    Origin: int16 * int16
    Target: int16 * int16
    Delay: float
}

type Message =
    | Connected of bool
    | New of Entity
    | MapChanged of string
    | Speed of Id * float
    | Position of Position
    | Movement of Movement
    | Health of Health
    | Attribute of (Attributes.Primary * int) list
    | AttributeCost of (Attributes.Primary * int) list
    | ExpNextJobLevel of int64
    | ExpNextBaseLevel of int64
    | JobExp of int64
    | BaseExp of int64
    //TODO: Packet OK, no stream
    | WeightSoftCap of int
    | NewSkill of Skill list
    //TODO: No packet, no stream
    | Zeny of int64

type Player = {
    Id: Id
    Name: string
    InitialMap: Yggdrasil.Navigation.Maps.Map
    Request: Request -> unit
    PacketStream: System.Reactive.Subjects.IConnectableObservable<uint16 * ReadOnlyMemory<byte>>
    MessageStream: System.Reactive.Subjects.IConnectableObservable<Message>
}