﻿module Frank.Tests.AsyncSeqSpecs

open System
open System.IO
open Frack.Collections
open NUnit.Framework

type Assert with
  static member IsEnded(result) = Assert.IsTrue(match result with Ended -> true | _ -> false)
  static member IsItem(result) = Assert.IsTrue(match result with Ended -> false | _ -> true)
  static member BytesInItem(item: byte ArraySegment AsyncSeqInner, numBytes) =
    Assert.IsTrue(match item with | Item(b, _) -> b.Count = numBytes | _ -> false)

let buffer = Array.zeroCreate 4096
let readInBlocks = AsyncSeq.readInBlocks

[<Test>]
let ``Reading an empty stream should return Ended``() =
  use stream = new MemoryStream(0)
  let aseq = stream |> readInBlocks buffer 1024
  let result = aseq |> Async.RunSynchronously
  Assert.IsEnded(result)

[<Test>]
let ``Reading a stream of size 1024 in 1024 blocks should return one Item and one Ended``() =
  let buffer = Array.zeroCreate 1024
  use stream = new MemoryStream(buffer)
  let aseq = stream |> readInBlocks buffer 1024

  let item, ended = async {
    let! item = aseq
    let! ended = aseq
    return (item, ended) } |> Async.RunSynchronously

  Assert.IsItem(item)
  Assert.BytesInItem(item, 1024)
  Assert.IsEnded(ended)

[<Test>]
let ``Reading a stream of size 1024 in 512 blocks should return two equally-sized Items and one Ended``() =
  let buffer = Array.zeroCreate 1024
  use stream = new MemoryStream(buffer)
  let aseq = stream |> readInBlocks buffer 512

  let item1, item2, ended = async {
    let! item1 = aseq
    let! item2 = aseq
    let! ended = aseq
    return (item1, item2, ended) } |> Async.RunSynchronously

  Assert.IsItem(item1)
  Assert.BytesInItem(item1, 512)
  Assert.IsItem(item2)
  Assert.BytesInItem(item2, 512)
  Assert.IsEnded(ended)

[<Test>]
let ``Reading a stream of size 1024 in 1000 blocks should return two unequally-sized Items and one Ended``() =
  let buffer = Array.zeroCreate 1024
  use stream = new MemoryStream(buffer)
  let aseq = stream |> readInBlocks buffer 1000

  let item1, item2, ended = async {
    let! item1 = aseq
    let! item2 = aseq
    let! ended = aseq
    return (item1, item2, ended) } |> Async.RunSynchronously

  Assert.IsItem(item1)
  Assert.BytesInItem(item1, 1000)
  Assert.IsItem(item2)
  Assert.BytesInItem(item2, 24)
  Assert.IsEnded(ended)

[<Test>]
let ``Reading a stream of size 1024 in 1024 blocks into a seq<ArraySegment<byte>> should return a same-size ArraySegment<byte>.``() =
  let test = "Hello"B
  use stream = new MemoryStream(test)
  let aseq = stream |> readInBlocks buffer 1024
  let result = aseq |> AsyncSeq.toSeq |> Async.RunSynchronously |> Seq.head
  Assert.AreEqual(test.Length, result.Count)

[<Test>]
let ``Reading a stream of size 2048 in 1024 blocks into a seq<ArraySegment<byte>> should return two ArraySegment<byte>.``() =
  let test = Array.zeroCreate 2048
  use stream = new MemoryStream(test)
  let aseq = stream |> readInBlocks buffer 1024

  let result = aseq |> (AsyncSeq.toSeq >> Async.RunSynchronously >> Array.ofSeq)

  Assert.AreEqual(2, result.Length)
  Assert.AreEqual(1024, result.[0].Count)
  Assert.AreEqual(1024, result.[1].Count)
