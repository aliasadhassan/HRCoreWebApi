namespace HR.Employee.API.Domain.Engagement;

public enum SurveyStatus : byte { Draft = 1, Open = 2, Closed = 3 }
public enum SurveyAudience : byte { Everyone = 1, Department = 2 }
public enum QuestionType : byte { Rating = 1, Nps = 2, YesNo = 3, Choice = 4, Text = 5 }
public enum RecognitionValue : byte { Teamwork = 1, Customer = 2, Innovation = 3, Ownership = 4, Leadership = 5, Helping = 6, Excellence = 7 }
