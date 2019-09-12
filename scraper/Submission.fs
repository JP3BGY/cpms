module Scraper.Submission
type SubmissionStatus = 
    |AC 
    |PE 
    |PAC  
    |RE  
    |TLE  
    |MLE  
    |WA 
    |CE  
    |WJ  
    |IG  
    |NaN

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
    |SubmissionStatus.NaN -> ""
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