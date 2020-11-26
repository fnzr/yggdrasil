module Yggdrasil.Handshake

open System
open System.Net
open System.Net.Sockets
open System.Runtime.CompilerServices
open System.Text
open NLog
open Yggdrasil.Utils
open Yggdrasil.StreamIO

let Logger = LogManager.GetCurrentClassLogger()

[<IsReadOnly; Struct>]
type Credentials = {
    AccountId: uint32
    LoginId1: uint32
    LoginId2: uint32
    Gender: byte
}

[<IsReadOnly; Struct>]
type SpawnInfo = {
    AccountId: uint32
    LoginId1: uint32
    Gender: byte
    CharId: int32
    //MapName: string
    MapServer: IPEndPoint
}

module LoginService =
    let private OtpTokenLogin: byte[] = Array.concat([|BitConverter.GetBytes(0xacfus); (Array.zeroCreate 66)|])

    let private LoginPacket (username: string) (password: string) =
                Array.concat([|
                    BitConverter.GetBytes(0x0064us);
                    BitConverter.GetBytes(0u);
                    FillBytes username 24;
                    FillBytes password 24;
                    BitConverter.GetBytes('0');
                |])

    let LoginSuccessParser data =
        let span = new ReadOnlySpan<byte>(data)
        let credentials = {
            AccountId = BitConverter.ToUInt32(span.Slice(8, 4))
            LoginId1 = BitConverter.ToUInt32(span.Slice(4, 4))
            LoginId2 = BitConverter.ToUInt32(span.Slice(12, 4))
            Gender = span.Slice(46, 1).[0]
            //WebAuthToken = Encoding.UTF8.GetString(span.Slice(47, 16))
        }
        let charServer = IPEndPoint(
                                       Convert.ToInt64(BitConverter.ToInt32(span.Slice(64, 4))),
                                       Convert.ToInt32(BitConverter.ToUInt16(span.Slice(68, 2)))                                       
                                   )
        charServer, credentials
         
    let rec Authenticate loginServer username password onLoginSuccess =
        let client = new TcpClient()
        client.Connect loginServer
        let stream = client.GetStream()
        
        let writer = GetWriter stream
        writer(OtpTokenLogin)
        
        Async.Start (async {
            let packetHandler = GetPacketHandler writer loginServer username password onLoginSuccess
            return! (GetReader stream packetHandler) Array.empty
            //stream.Dispose()
        })

    and GetPacketHandler writer loginServer username password onLoginSuccess =
        fun (packetType: uint16) (data: byte[]) ->
            match packetType with
            | 0x81us ->
                match data.[2] with
                | 8uy -> Logger.Info("Already logged in. Retrying.")
                         Authenticate loginServer username password onLoginSuccess
                | _ -> Logger.Error("Login refused. Code: {errorCode:d}", data.[2]) 
            | 0xae3us -> writer(LoginPacket username password)
            | 0xac4us -> onLoginSuccess (LoginSuccessParser data)
            | unknown -> Logger.Error("Unknown LoginServer packet {packetType:X}", unknown)

module CharacterService =

    let private RequestToConnect (credentials: Credentials) = Array.concat([|
        BitConverter.GetBytes(0x65us)
        BitConverter.GetBytes(credentials.AccountId)
        BitConverter.GetBytes(credentials.LoginId1)
        BitConverter.GetBytes(credentials.LoginId2)
        BitConverter.GetBytes(0us)
        [| credentials.Gender |]
    |])

    let private CharSelect (slot: byte) = Array.concat[|    
        BitConverter.GetBytes(0x0066us)
        [| slot |]
    |]

    let ZoneInfoParser data accountId loginId1 gender =
        let span = new ReadOnlySpan<byte>(data)
        {
            AccountId = accountId
            LoginId1 = loginId1
            Gender = gender
            CharId = BitConverter.ToInt32(span.Slice(2, 4))
            //MapName =  Encoding.UTF8.GetString(span.Slice(6, 16))
            MapServer = IPEndPoint(
                           Convert.ToInt64(BitConverter.ToInt32(span.Slice(22, 4))),
                            Convert.ToInt32(BitConverter.ToUInt16(span.Slice(26, 2)))
                        )            
        }
        

    let GetPacketHandler writer accountId loginId1 gender characterSlot onCharacterSelected =
        fun (packetType: uint16) (data: byte[]) ->
            let mutable name = ""
            match packetType with
            | 0x82dus | 0x9a0us | 0x20dus | 0x8b9us -> ()
            | 0x6bus ->
                name <- Encoding.UTF8.GetString(data.[115..139])
                writer(CharSelect characterSlot)            
            | 0xac5us -> onCharacterSelected (ZoneInfoParser data accountId loginId1 gender) name
            | 0x840us -> Logger.Error("Map server unavailable")
            | unknown -> Logger.Error("Unknown CharServer packet {packetType:X}", unknown)
        
    let SelectCharacter charServer credentials characterSlot onCharacterSelected =
        let client = new TcpClient()
        client.Connect charServer
        let stream = client.GetStream()   
     
        let writer = GetWriter stream
        writer(RequestToConnect credentials)

        //account_id feedback
        ReadBytes stream 4 |> ignore
        
        Async.Start (async {
            let packetHandler = GetPacketHandler writer credentials.AccountId credentials.LoginId1 credentials.Gender characterSlot onCharacterSelected
            return! (GetReader stream packetHandler) Array.empty
            //stream.Dispose()
    })
        
module ZoneService =
    
    open Yggdrasil.ZoneService
    
    let private WantToConnect zoneInfo =
        Array.concat [|
            BitConverter.GetBytes(0x0436us)
            BitConverter.GetBytes(zoneInfo.AccountId)
            BitConverter.GetBytes(zoneInfo.CharId)
            BitConverter.GetBytes(zoneInfo.LoginId1)
            BitConverter.GetBytes(1)
            [| zoneInfo.Gender |]
        |]
    
    let Connect zoneInfo messageStore =
        let client = new TcpClient()
        client.Connect(zoneInfo.MapServer)
        
        let stream = client.GetStream()
        
        let writer = GetWriter stream
        writer <| WantToConnect zoneInfo
        
        let mailbox = Agent.CreateAgentMailbox zoneInfo.AccountId messageStore
        
        Async.Start (async {            
            let packetHandler = ZonePacketHandler mailbox writer
            Async.Ignore <| (GetReader stream packetHandler) Array.empty |> ignore
            stream.Dispose()
        })
        mailbox