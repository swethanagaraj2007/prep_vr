using System.Collections.Generic;
using UnityEngine;

public class InterviewSession : MonoBehaviour
{
    public CandidateProfile candidateProfile;
    public Dictionary<string, object> interviewContext = new Dictionary<string, object>();
    public List<InterviewResponse> responses = new List<InterviewResponse>();
    public InterviewEvaluation evaluation;

    public void SetCandidateProfile(CandidateProfile profile)
    {
        candidateProfile = profile;
    }

    public void SetInterviewContext(Dictionary<string, object> context)
    {
        interviewContext = context ?? new Dictionary<string, object>();
    }

    public void AddResponse(string question, string answer)
    {
        InterviewResponse response = new InterviewResponse();

        response.questionNumber = responses.Count + 1;
        response.question = question;
        response.answer = answer;

        responses.Add(response);
    }

    public List<InterviewResponse> GetResponses()
    {
        return responses;
    }

    public void SetEvaluation(InterviewEvaluation result)
    {
        evaluation = result;
    }
}