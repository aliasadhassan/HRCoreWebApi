namespace HR.Employee.API.Application.Policies;

using HR.Employee.API.Domain.Policies;

/// <summary>
/// Shuru karne ke liye aam policies (draft). HR inhein apni company ke hisaab se badal kar publish karta hai.
/// Text halka markdown hai: "# " heading, "- " bullet, khaali line = naya paragraph.
/// </summary>
internal static class StarterPolicies
{
    public static readonly PolicyDetails[] All =
    [
        new("Code of conduct", PolicyCategory.Conduct,
            "How we treat each other, customers and company property.",
            """
            # Why this policy exists
            We want everyone to do their best work in a place that is honest, respectful and safe.

            # What we expect
            - Treat colleagues, candidates and customers with respect.
            - Be honest in your work, your records and your claims.
            - Avoid conflicts of interest. Tell your manager if one comes up.
            - Look after company property and information.
            - Follow the law and the other company policies.

            # Raising a concern
            Talk to your manager or HR, or raise a confidential request on the Requests page. Nobody will be treated badly for raising a concern in good faith.

            # If the policy is broken
            Breaking this policy can lead to disciplinary action, up to and including dismissal.
            """,
            null, null, null, true, 14, 12),
        new("Anti-harassment and equal opportunity", PolicyCategory.Conduct,
            "Zero tolerance for harassment, bullying and discrimination.",
            """
            # Our commitment
            Everyone is treated fairly, whatever their gender, religion, ethnicity, age, disability or background.

            # What is not allowed
            - Harassment of any kind, including sexual harassment.
            - Bullying, threats or humiliating behaviour, in person or online.
            - Decisions about hiring, pay or promotion based on anything other than merit.

            # How to report
            Raise a confidential request on the Requests page or speak to HR directly. Reports are investigated quickly and privately.
            """,
            null, null, null, true, 14, 12),
        new("Leave and holidays", PolicyCategory.TimeOff,
            "Types of leave, how to apply and how approvals work.",
            """
            # Applying for leave
            - Apply on the Leaves page before your leave starts, except in an emergency.
            - Your manager approves or rejects the request.
            - Balances and the holiday calendar are shown on the Leaves page.

            # Sick leave
            Tell your manager as early as you can on the first day. A medical certificate may be needed for longer absences.

            # Unpaid leave
            Leave beyond your balance is unpaid and is deducted in payroll.
            """,
            null, null, null, false, null, 12),
        new("IT and acceptable use", PolicyCategory.Security,
            "Using laptops, email, systems and the internet safely.",
            """
            # Your accounts
            - Keep your passwords private and turn on two-step sign-in where offered.
            - Lock your screen when you step away.

            # Company devices and data
            - Use company devices mainly for work.
            - Do not install unapproved software.
            - Report a lost device or a suspicious email to IT straight away.

            # Monitoring
            Company systems may be monitored to keep them secure, within the law.
            """,
            null, null, null, true, 14, 12),
        new("Data protection and confidentiality", PolicyCategory.Legal,
            "Handling personal and confidential information.",
            """
            # What counts as confidential
            Employee records, salaries, customer data, plans and anything not already public.

            # Rules
            - Only access the data you need for your job.
            - Do not share confidential data outside the company without approval.
            - Store files in approved company systems, not personal accounts.

            # After you leave
            Your duty to keep company information confidential continues after your employment ends.
            """,
            null, null, null, true, 14, 12),
        new("Health and safety at work", PolicyCategory.HealthSafety,
            "Keeping the workplace safe and reporting incidents.",
            """
            # Everyone's responsibility
            - Follow safety instructions and signs.
            - Keep walkways and fire exits clear.
            - Report hazards, accidents and near misses to your manager the same day.

            # Emergencies
            Know where the nearest fire exit, assembly point and first aid kit are.
            """,
            null, null, null, false, null, 12)
    ];
}
