let meta = #{
    name: "ofs-review",
    description: "Read-only review of an OpenForgeSelf diff or path",
    when_to_use: "Review a path, git ref, or diff range before calling the change done",
    phases: [
        #{ title: "Review", detail: "three read-only reviewers" },
        #{ title: "Verify", detail: "one skeptic per finding" },
    ],
};

let findings_schema = #{
    "type": "object",
    "required": ["findings"],
    "properties": #{
        "findings": #{
            "type": "array",
            "maxItems": 6,
            "items": #{
                "type": "object",
                "required": ["file", "issue", "severity"],
                "properties": #{
                    "file": #{ "type": "string" },
                    "issue": #{ "type": "string" },
                    "severity": #{ "type": "string" },
                },
            },
        },
    },
};

let verdict_schema = #{
    "type": "object",
    "required": ["real", "reason", "evidence"],
    "properties": #{
        "real": #{ "type": "boolean" },
        "reason": #{ "type": "string" },
        "evidence": #{ "type": "string" },
    },
};

let target = if args == () { () } else { args.target };
if target == () {
    pause("verification", "Pass args.target: a path, a git ref, or a range such as HEAD.");
}

phase("Review");
let dimensions = ["correctness bugs", "host and process safety", "missing tests"];
let jobs = [];
for d in dimensions {
    let prompt = "Review OpenForgeSelf target `" + target + "` for " + d + ". ";
    prompt += "Read the real files with read_file and grep. If the target is a git ref or range, run git diff in pwsh. Do not edit files. Do not start, stop, or kill any process, including port 51888. ";
    prompt += "Host safety: repo scripts must use pwsh, not powershell 5.1; the user host stays untouched; local stack only via scripts/dev-stack.ps1. ";
    prompt += "Missing tests: a behavior change without a vitest, dotnet test, or Playwright spec. ";
    prompt += "Return at most 6 findings with file, issue, and severity high or low. An empty list is valid only after you have read the code or the diff.";
    jobs.push(#{
        prompt: prompt,
        label: "review:" + d,
        capability_mode: "execute",
        output_schema: findings_schema,
    });
}
let results = parallel(jobs);

let findings = [];
for r in results {
    if r != () && r.success && r.output != () && r.output.findings != () {
        for f in r.output.findings {
            if f != () && f.file != () && f.issue != () {
                findings.push(f);
            }
        }
    }
}

if findings.len() == 0 {
    complete(#{ summary: "No findings.", confirmed: [] });
}

let capped = [];
let seen = 0;
for f in findings {
    if seen < 8 {
        capped.push(f);
    }
    seen += 1;
}
if seen > 8 {
    log("verification capped at 8, dropped " + (seen - 8).to_string());
}

phase("Verify");
let vjobs = [];
for f in capped {
    let prompt = "Adversarially verify this OpenForgeSelf review finding by reading the code: ";
    prompt += f.issue + " in " + f.file + ". ";
    prompt += "Set real=true only with evidence you inspected yourself. Otherwise set real=false. Do not edit files or touch running processes.";
    vjobs.push(#{
        prompt: prompt,
        label: "verify:" + f.file,
        capability_mode: "execute",
        output_schema: verdict_schema,
    });
}
let verdicts = parallel(vjobs);

let confirmed = [];
let i = 0;
for v in verdicts {
    if v != () && v.success && v.output != () && v.output.real == true && v.output.evidence != () && v.output.evidence != "" {
        confirmed.push(capped[i]);
    }
    i += 1;
}
log(confirmed.len().to_string() + "/" + capped.len().to_string() + " findings survived verification");
complete(#{
    summary: confirmed.len().to_string() + " confirmed findings",
    confirmed: confirmed,
});
