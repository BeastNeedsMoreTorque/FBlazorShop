﻿module Domain

open FBlazorShop.App.Model
open Akkling
open Akkling.Persistence
open AkklingHelpers
open Actor
open Akka
open Common
open Akka.Cluster.Sharding
open Serilog
module Order =
    type Command = PlaceOrder of Order

    type Event =
        | OrderPlaced of Order
        | OrderRejected of Order * reason : string

    let actorProp (mediator : IActorRef<_>) (mailbox : Eventsourced<_>)=
        let rec set (state : Order option * int)=
            actor {
                let! msg = mailbox.Receive()
                Log.Information("Message {@MSG}", box msg)
                match msg, state with
                | Recovering mailbox (Event {Event = OrderPlaced o; Version = version}), _ ->
                    return! (o |> Some, version) |> set

                | Command{Command = PlaceOrder o; CorrelationId = ci}, (None,version) ->
                    //call saga starter and wait till it responds
                    let event =  Event.toEvent ci (o |> OrderPlaced ) (version + 1)
                    SagaStarter.toSendMessage mediator mailbox.Self event
                    return! event |> Event |> Persist

                | Command {Command = PlaceOrder o; CorrelationId = ci}, (Some _, version) ->
                    //An order can be placed once only
                    mailbox.Sender() <! ( (Event.toEvent ci (OrderRejected(o,"duplicate")) version) |> Event)

                | Persisted mailbox (Event({Event = OrderPlaced o ; Version = v} as e)), _ ->
                    Log.Information "persisted"

                    SagaStarter.publishEvent mailbox mediator e
                    return! ((o |> Some), v) |> set
                | _ -> return Unhandled
            }
        set ( None ,0)
    let init =
        AkklingHelpers.entityFactoryFor Actor.system "Order"
            <| propsPersist (actorProp (typed Actor.mediator))
            <| false

    let factory entityId =
           init.RefFor AkklingHelpers.DEFAULT_SHARD entityId


module Delivery =
    type Command = StartDelivery of Order

    type Event =
        | Delivered of Order
      //  | Deliver
      //  | OrderRejected of Order * reason : string
    type State =
        | NotStarted
        | Delivering of LatLong * Order
        | DeliveryCompleted of Order


    let actorProp (mediator : IActorRef<_>) (mailbox : Eventsourced<_>)=
        let rec set (state : State * int) =
            actor {
                let! msg = mailbox.Receive()
                match msg, state with
                | Recovering mailbox (Event {Event = Delivered o; Version = v}), _ ->
                    return! (o |> DeliveryCompleted,v) |> set

                | Command{Command = StartDelivery o; CorrelationId = ci}, (NotStarted,v) ->
                    //call saga starter and wait till it responds
                    let event = Event.toEvent ci ( o |> Delivered ) (v + 1)
                    SagaStarter.toSendMessage mediator mailbox.Self event
                    return! event |> Event |> Persist

                //| Command {Command = PlaceOrder o; CorrelationId = ci}, Some _ ->
                //    //An order can be placed once only
                //    mailbox.Sender() <! (OrderRejected(o,"duplicate") |> Event.toEvent ci |> Event)

                | Persisted mailbox (Event({Event = Delivered o ; Version  = v} as e)), _ ->
                   SagaStarter.publishEvent mailbox mediator e
                   return! set ((DeliveryCompleted o), v)
                | _ -> return Unhandled
            }
        set (NotStarted, 0)
    let init =
        Log.Information "order init"

        AkklingHelpers.entityFactoryFor Actor.system "Delivery"
            <| propsPersist (actorProp (typed Actor.mediator))
            <| false

    let factory entityId =
           init.RefFor AkklingHelpers.DEFAULT_SHARD entityId

module OrderSaga =
    type State =
        | Started
        | ProcessingOrder of Order
        | OutForDelivery of Order
        | Delivered of Order

    type Event =
        | StateChanged of State
        with interface IDefaultTag

    let actorProp (mediator : IActorRef<_>)(mailbox : Eventsourced<obj>)=
        let rec set (state : State) =
            actor {
                let! msg = mailbox.Receive()
                match msg, state with
                | SagaStarter.SubscrptionAcknowledged mailbox _, _  ->
                    // notify saga starter about the subscription completed
                    SagaStarter.cont mediator
                    return! set state

                | PersistentLifecycleEvent ReplaySucceed ,_->
                    SagaStarter.subscriber mediator mailbox
                    //  take recovery action for the final state
                    return! set state

                | Recovering mailbox (:? Event as e), _ ->
                    //replay the recovery
                    match e with
                    | StateChanged s -> return! set s

                | Persisted mailbox (:? Event as e ), _->
                    match e with
                    //take entry actions of new state
                    | StateChanged (ProcessingOrder o) ->
                        let rawGuid = (mailbox.Self.Path.Name |> SagaStarter.toRawoGuid)
                        let delivery = Delivery.factory <| "Delivery_" + rawGuid
                        delivery<!
                            ({ Command =  Delivery.StartDelivery o;
                                CreationDate = System.DateTime.Now;
                                CorrelationId = Some rawGuid} |> Common.Command)
                        return! set state

                    | _ -> return! set state
                | :? Common.Event<Order.Event> as orderEvent, _ ->
                    // decide new state
                    match orderEvent with
                    | {Event = Order.OrderPlaced o } ->
                      let state = ProcessingOrder o
                      return! Persist(StateChanged (state)|>box)

                    | _ -> return! set state

                | :? Common.Event<Delivery.Event> as deliveryEvent, _ ->
                    // decide new state
                    match deliveryEvent with
                    | {Event = Delivery.Delivered o } ->
                        mailbox.Parent() <! Passivate(Actor.PoisonPill.Instance)


                | _ -> return! set state
            }
        set Started

    let init =
        (AkklingHelpers.entityFactoryFor Actor.system "OrderSaga"
        <| propsPersist (actorProp(typed Actor.mediator))
        <| true)

    let factory entityId =
        init.RefFor AkklingHelpers.DEFAULT_SHARD entityId

let sagaCheck (o : obj)=
    match o with
    | :? Common.Event<Order.Event> as e ->
        match e with
        | {Event = Order.OrderPlaced _  }  -> Some OrderSaga.factory
        | _ -> None
    | _ -> None

let init () =
    SagaStarter.init Actor.system Actor.mediator sagaCheck
    Order.init |> printf "%A"
    Delivery.init |> printf "%A"
    OrderSaga.init |> printf "%A"
    System.Threading.Thread.Sleep(1000)

