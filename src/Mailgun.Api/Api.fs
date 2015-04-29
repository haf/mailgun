module Mailgun.Api

open System
open System.IO
open System.Net.Mail
open NodaTime
open HttpClient

type Configured =
  { apiKey : string }

type MailBody =
  | HtmlBody of string
  | TextBody of string
  | TextAndHtmlBody of string * string

type Message =
  { from        : MailAddress
    ``to``      : MailAddress list
    cc          : MailAddress list
    bcc         : MailAddress list
    /// always UTF8 encoded
    subject     : string
    /// always UTF8 encoded
    body        : MailBody
    attachments : File list }

type Header = string * string

type Var = string * string

type SendOpts =
  { domain         : string
    tag            : string option
    campaign       : string option
    dkim           : bool
    /// max +3 days
    deliveryTime   : Instant option
    testMode       : bool
    tracking       : bool
    trackingClicks : bool
    trackingOpens  : bool
    extraHeaders   : Header list
    extraVars      : Var list }

  static member Create(?domain : string) =
    { domain         = defaultArg domain "example.com"
      tag            = None
      campaign       = None
      dkim           = true
      deliveryTime   = None
      testMode       = false
      tracking       = true
      trackingClicks = false
      trackingOpens  = false
      extraHeaders   = []
      extraVars      = [] }

type ApiResponse<'T> =
  | ServerError of string * Response
  | ClientError of string * Response
  | NotFoundResult of string
  | Result of 'T * Response
  //| PaginatedResult of 'T * PageState * HttpClient.Response
  | Timeout of Duration

module internal Impl =
  let BaseUri = "https://api.mailgun.net/v3"

  let resource (r : string) =
    Uri (String.Concat [ BaseUri; "/"; r.TrimStart('/') ])

  let collection domain (coll : string) =
    resource (String.Concat [ domain; "/"; coll.TrimStart('/') ])

  let mailgunRequest settings methd resource =
    createRequest methd resource
    |> withBasicAuthentication "api" settings.apiKey

  let asString = function
    | ResponseString str -> str
    | ResponseBytes bs -> System.Text.Encoding.UTF8.GetString bs

  let getMailgunApiResponse (req : Request) =
    async {
      let! resp = req |> getResponseAsync
      match resp.StatusCode with
      | x when x < 300 ->
        return Result ("", resp)
      | x when x >= 300 && x < 400 ->
        return ClientError ("unexpected 3xx code from server", resp)
      | x when x >= 400 && x < 500 ->
        let err = resp.EntityBody |> Option.get
        return ClientError ("Request Error from server: " + asString err, resp)
      | _ ->
        let err = resp.EntityBody |> Option.get
        return ServerError ("Server error: " + asString err, resp)
    }

module Messages =
  open Chiron
  open Chiron.Operators
  open Impl

  type SendResponse =
    { message : string
      id      : string }

    static member FromJson (_ : SendResponse) =
      (fun m i ->
        { message = m
          id      = i })
      <!> Json.read "message"
      <*> Json.read "id"

    static member ToJson (resp : SendResponse) =
      Json.write "message" resp.message
      *> Json.write "id" resp.id

  let private generateSendBody settings m =
    let emailNameValue formControlName email =
      NameValue {
        name = "to"
        value = (email.ToString())
      }

    let optRaw nonPrefixName value =
      NameValue { name = "o:" + nonPrefixName; value = value }

    let opt nonPrefixName = function
      | None -> failwith "programming error"
      | Some x -> NameValue { name = "o:" + nonPrefixName; value = x }

    let optb nonPrefixName = function
      | true -> NameValue { name = "o:" + nonPrefixName; value = "yes" }
      | false -> NameValue { name = "o:" + nonPrefixName; value = "no" }

    BodyForm
      [ // data:
        yield NameValue { name ="from"; value = m.from.ToString() }
        yield! m.``to`` |> List.map (emailNameValue "to")
        yield! m.cc |> List.map (emailNameValue "cc")
        yield! m.bcc |> List.map (emailNameValue "bcc")
        yield NameValue { name = "subject"; value = m.subject }
        match m.body with
        | TextBody text -> yield NameValue { name = "text"; value = text }
        | HtmlBody html -> yield NameValue { name = "html"; value = html }
        | TextAndHtmlBody (text, html) ->
          yield NameValue { name = "text"; value = text }
          yield NameValue { name = "html"; value = html }
        yield! m.attachments |> List.map(fun file -> FormFile ("attachment", file))

        // settings:
        if Option.isSome settings.tag then
          yield opt "tag" settings.tag
        if Option.isSome settings.campaign then
          yield opt "campaign" settings.campaign
        yield optb "dkim" settings.dkim
        if Option.isSome settings.deliveryTime then
          let instant = settings.deliveryTime |> Option.get
          yield optRaw "deliverytime" (instant.RFC2822UTC())
        if settings.testMode then yield optb "testmode" true
        yield optb "tracking" settings.tracking
        yield optb "tracking-clicks" settings.trackingClicks
        yield optb "tracking-opens" settings.trackingOpens
        for (headerName, headerValue) in settings.extraHeaders do
          yield NameValue { name = "h:" + headerName; value = headerValue }
        for (varName, varValue) in settings.extraVars do
          yield NameValue { name = "v:" + varName; value = varValue }
      ]

  let send config sendOpts (m : Message) =
    mailgunRequest config Post (collection sendOpts.domain "messages")
    |> withBody (generateSendBody sendOpts m)
    |> getMailgunApiResponse
