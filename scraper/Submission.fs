module Scraper.Submission
type SubmissionStatus = 
    |AC = 0
    |PE = 1
    |PAC = 2
    |WA = 3
    |RE = 4 
    |TLE = 5 
    |MLE = 6 
    |CE = 7 
    |WJ = 8 
    |IG = 9 
    |NaN = 10

let submissionStatusDescription ss =
    match ss with
    |SubmissionStatus.AC -> "Accepted"
    |SubmissionStatus.WA -> "Wrong Answer"
    |SubmissionStatus.TLE -> "Time Limit Exceeded"
    |SubmissionStatus.MLE -> "Memory Limit Exceeded"
    |SubmissionStatus.RE -> "Runtime Error"
    |SubmissionStatus.PAC -> "Partially Accepted"
    |SubmissionStatus.CE -> "Compile Error"
    |SubmissionStatus.PE -> "Presentation Error"
    |SubmissionStatus.WJ -> "Waiting for Judge"
    |SubmissionStatus.IG -> "IGnored for some reason"
    |_ -> "No such submission status found"
let submissionStatusToString ss =
    match ss with
    |SubmissionStatus.AC -> "AC"
    |SubmissionStatus.WA -> "WA"
    |SubmissionStatus.TLE -> "TLE"
    |SubmissionStatus.MLE -> "MLE"
    |SubmissionStatus.RE -> "RE"
    |SubmissionStatus.PAC -> "PAC"
    |SubmissionStatus.CE -> "CE"
    |SubmissionStatus.PE -> "PE"
    |SubmissionStatus.WJ -> "WJ"
    |SubmissionStatus.IG -> "IG"
    |_-> ""
let statusToSubmissionStatus ss =
    match ss with
    |"AC" ->
        SubmissionStatus.AC 
    |"WA" ->
        SubmissionStatus.WA 
    |"TLE" ->
        SubmissionStatus.TLE
    |"MLE" ->
        SubmissionStatus.MLE
    |"RE" ->
        SubmissionStatus.RE 
    |"PAC" ->
        SubmissionStatus.PAC
    |"CE" ->
        SubmissionStatus.CE 
    |"PE" ->
        SubmissionStatus.PE 
    |"WJ" ->
        SubmissionStatus.WJ 
    |"IG" ->
        SubmissionStatus.IG 
    |_ -> 
        SubmissionStatus.NaN