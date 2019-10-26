﻿module Seed

open FBlazorShop.EF
open FBlazorShop.App.Model

let initialize (db : PizzaStoreContext) =
    let specials = [
        {
            Id = 0
            Name = "Basic Cheese Pizza"
            Description = "It's cheesy and delicious. Why wouldn't you want one?"
            BasePrice = 9.99m
            ImageUrl = "img/pizzas/cheese.jpg"
        }
    ]
    db.Specials.AddRange specials
    db.SaveChanges() |> ignore

