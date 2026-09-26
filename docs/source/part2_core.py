"""Part II — The stochastic core: MiniNeuralNetwork, dropout, MC Dropout, Frozen Snapshot."""

import math

from figures import DROPOUT, HIDDEN_B, HIDDEN_W, OUT_B, OUT_W, exact_mc, forward, hidden, mask_distribution


def build(s, F):
    s.part("II", "The stochastic core",
           "A neural network small enough to compute by hand: where its answers come from, where its "
           "randomness comes from, how to measure its confidence, and how to freeze it.")

    # ---------------------------------------------------------------- 5
    s.chapter("5", "A neuron from first principles",
              "Before the network, one neuron. If you understand a weighted sum and a squashing function, "
              "you understand everything the stochastic core does.")
    s.figure(F["neuron"], "Figure 5.1 — One hidden neuron of the POC: a weighted sum of the two scaled "
             "inputs plus a bias, passed through tanh.", width=0.85)
    s.p(
        "A neuron does two things. First it computes a <b>weighted sum</b> of its inputs plus a constant "
        "called the <b>bias</b>: <i>z = w<sub>1</sub>x<sub>1</sub> + w<sub>2</sub>x<sub>2</sub> + b</i>. The "
        "weights say how much each input matters and in which direction; the bias shifts the point at which "
        "the neuron starts to respond. This part is ordinary linear algebra — the same operation as a "
        "weighted average or a linear regression (book, Chapter 4).",
        "Second, it passes <i>z</i> through an <b>activation function</b>. Without this step a network of "
        "any depth would collapse into one big linear function and could never represent a curved response "
        "(the XOR problem of the book's Chapter 5 is the classic proof). The POC uses two activations:",
    )
    s.figure(F["activations"], "Figure 5.2 — The two activation functions used in the POC.", width=0.9)
    s.bullets([
        "<b>tanh</b> in the hidden layer. It maps any real number to (−1, 1), is roughly linear near zero and "
        "saturates for large |z|. Positive and negative outputs let hidden neurons push the final answer "
        "either way.",
        "<b>sigmoid</b> σ(z) = 1 / (1 + e<super>−z</super>) at the output. It maps any real number to "
        "(0, 1), which is exactly the range of a valve opening from fully closed to fully open. The value "
        "before the sigmoid is called the <b>logit</b>.",
    ])
    s.callout("key", "Scaling the inputs", [
        "The network never sees raw engineering units. Temperature is divided by 100 and pressure by 10, so "
        "a typical reading lands in the range 0 to 1. Without this, a pressure of 8 and a temperature of 80 "
        "would enter the weighted sum with wildly different magnitudes and the neuron would be dominated by "
        "whichever unit happens to produce bigger numbers. Input scaling is the first thing to check when a "
        "network behaves strangely.",
    ])

    # ---------------------------------------------------------------- 6
    s.chapter("6", "MiniNeuralNetwork, layer by layer",
              "Two inputs, four hidden neurons, one output, seventeen numbers. We compute an answer by hand.")
    s.figure(F["network"], "Figure 6.1 — The 2-4-1 network. Blue connections have positive weights, orange "
             "negative; thickness is proportional to |w|. Each hidden neuron can be dropped with p = 0.2.")
    s.p(
        "The <font face='Mono'>MiniNeuralNetwork</font> is a <b>multilayer perceptron</b> (MLP) with the "
        "shape 2-4-1. Its entire knowledge is the seventeen numbers in the table below: eight hidden "
        "weights, four hidden biases, four output weights and one output bias. They were set by hand to give "
        "a smooth, plausible response: the valve opens more as pressure rises and, more gently, as "
        "temperature rises. In a real system these numbers would come from training (book, Chapter 7). "
        "Nothing in the architecture around the network depends on how they were obtained.",
    )
    rows = [["Neuron", "w (temperature)", "w (pressure)", "bias", "w to output"]]
    for h in range(4):
        rows.append([f"h{h + 1}", f"{HIDDEN_W[h][0]:+.1f}", f"{HIDDEN_W[h][1]:+.1f}", f"{HIDDEN_B[h]:+.1f}", f"{OUT_W[h]:+.1f}"])
    rows.append(["output", "", "", f"{OUT_B:+.1f}", ""])
    s.table(rows, [0.2, 0.2, 0.2, 0.2, 0.2])
    s.section("A worked forward pass: 60 °C, 5 bar")
    x, zs, a = hidden(60, 5)
    s.p(
        f"<b>Step 1 — scale.</b> x<sub>1</sub> = 60/100 = {x[0]:.1f}, x<sub>2</sub> = 5/10 = {x[1]:.1f}.",
        "<b>Step 2 — hidden layer.</b> For each neuron compute z = w<sub>1</sub>x<sub>1</sub> + "
        "w<sub>2</sub>x<sub>2</sub> + b and a = tanh(z):",
    )
    rows = [["Neuron", "z", "a = tanh(z)", "contribution if kept: w·a / 0.8"]]
    for h in range(4):
        rows.append([f"h{h + 1}", f"{HIDDEN_B[h]:+.1f} {HIDDEN_W[h][0]:+.1f}×{x[0]:.1f} {HIDDEN_W[h][1]:+.1f}×{x[1]:.1f} = {zs[h]:+.4f}",
                     f"{a[h]:+.4f}", f"{OUT_W[h] * a[h] / (1 - DROPOUT):+.4f}"])
    s.table(rows, [0.12, 0.44, 0.18, 0.26])
    logit, y = forward(60, 5, (1, 1, 1, 1))
    terms = "".join(f" {'−' if c < 0 else '+'} {abs(c):.4f}" for c in (OUT_W[h] * a[h] / (1 - DROPOUT) for h in range(4)))
    s.p(
        "<b>Step 3 — output.</b> With all four neurons kept, the logit is the output bias plus the four "
        "contributions:",
    )
    s.code(f"logit = −1.2{terms} = {logit:+.4f}\nσ({logit:.4f}) = 1 / (1 + e^{-logit:.4f}) = {y:.4f}")
    s.p(
        f"The network proposes a valve opening of <b>{100 * y:.1f}%</b>.",
        "Why the division by 0.8? That is <b>inverted dropout</b>, explained in the next chapter. It keeps "
        "the <i>expected</i> contribution of each neuron the same whether or not dropout is active.",
    )
    s.callout("try", "Compute a second reading yourself", [
        "Repeat the three steps for 85 °C and 7 bar. You should find hidden activations of about 0.782, "
        "−0.065, 0.688 and −0.197, a logit of 1.084 with all neurons kept, and an opening of 74.7%. "
        "Chapter 8 explains why this reading nevertheless ends up escalated to a human.",
    ])
    s.figure(F["policy_map"], heading="What the network has learned: the policy surface", caption="Figure 6.2 — Expected valve opening over the whole operating envelope. Hatched "
             "regions are where protection islands take control and the network's answer is not used at all. "
             "The orange contour marks the Shield's 75% cap.")
    s.p(
        "Plotting the network's expected output over every temperature and pressure gives its <b>policy "
        "surface</b>. Two things are worth noticing. First, it is smooth: nearby readings produce nearby "
        "answers, which is what we want from a controller. Second, in the upper band — high pressure — the "
        "network's own answer climbs above 75%. This is precisely where the architecture stops trusting it: "
        "above 8 bar the pressure-relief island takes over, and anywhere else the Shield caps the command. "
        "The network is free to be wrong in those regions, because its answer is never applied there.",
    )

    # ---------------------------------------------------------------- 7
    s.chapter("7", "Dropout: where the randomness comes from",
              "Four coin flips per call. Sixteen possible networks. One distribution of answers.")
    s.p(
        "<b>Dropout</b> was invented as a training trick (book, §7.6.4): randomly switching off neurons "
        "during training prevents the network from relying too heavily on any single one, which improves "
        "generalisation. Normally it is turned off at inference. The POC deliberately leaves it <b>on</b>, "
        "for two reasons. It reproduces, in the smallest possible system, the exact problem of §12.2 — "
        "a correct model that answers the same question differently — and it gives us, for free, a way of "
        "measuring the model's uncertainty (Chapter 8).",
        f"On every call, each hidden neuron is independently dropped with probability p = {DROPOUT}. A "
        "dropped neuron contributes nothing; a kept one contributes w·a/(1 − p). The division by (1 − p) = "
        "0.8 is <b>inverted dropout</b>: because a neuron is present only 80% of the time, scaling its "
        "contribution up by 1/0.8 keeps its <i>average</i> contribution equal to w·a.",
    )
    s.section("Enumerating every possible network")
    s.p(
        "Four neurons, each kept or dropped, give 2<super>4</super> = 16 possible masks. The probability "
        "of a mask with k kept neurons is 0.8<super>k</super>·0.2<super>4−k</super>. For the reading 60 °C, "
        "5 bar we can therefore compute the <i>exact</i> distribution of the network's answer:",
    )
    rows = [["Mask (h1 h2 h3 h4)", "Probability", "Output", "Mask", "Probability", "Output"]]
    dist = sorted(mask_distribution(60, 5), key=lambda r: -r[1])
    for i in range(8):
        l, r = dist[i], dist[i + 8]
        rows.append(["".join(map(str, l[0])), f"{l[1]:.4f}", f"{l[2]:.4f}", "".join(map(str, r[0])), f"{r[1]:.4f}", f"{r[2]:.4f}"])
    s.table(rows, [0.2, 0.15, 0.15, 0.2, 0.15, 0.15])
    s.figure(F["mask_distribution"], "Figure 7.1 — The exact distribution of the network's answer at 60 °C, "
             "5 bar. The blue bar is the ‘all neurons kept’ network (41% of calls); every other bar is a "
             "network with one or more neurons dropped.")
    mean, sd, hw = exact_mc(60, 5)
    lo = min(r[2] for r in dist)
    hi = max(r[2] for r in dist)
    s.p(
        f"Read the figure as an engineer would read a histogram of repeated measurements. The answers range "
        f"from {100 * lo:.1f}% to {100 * hi:.1f}%. The most likely single answer (all neurons kept) is "
        f"{100 * dist[0][2]:.1f}%, but it occurs only 41% of the time. The expected value is "
        f"{100 * mean:.1f}% and the standard deviation is {100 * sd:.1f} percentage points. If this network "
        f"drove the valve directly, the valve could swing between {100 * lo:.0f}% and {100 * hi:.0f}% from one "
        "cycle to the next with the plant in a perfectly steady state.",
    )
    s.callout("key", "Stochastic ≠ broken", [
        "Every one of these sixteen networks is a legitimate instance of the same model. The spread is not "
        "noise added from outside; it is the model's own internal disagreement. That is exactly why it can "
        "be used as a measure of confidence in the next chapter.",
    ])

    # ---------------------------------------------------------------- 8
    s.chapter("8", "MC Dropout: turning randomness into a confidence measure",
              "Ask the same question 200 times. The average is the answer; the spread is how much to trust it. "
              "(Book, §13.6.)")
    s.p(
        "<b>Monte Carlo (MC) Dropout</b> is the book's practical alternative to full Bayesian neural "
        "networks (§13.6). Instead of one forward pass, run many with dropout left on, and treat the "
        "resulting sample as an empirical distribution. The POC runs <b>200 passes</b> per control cycle and "
        "computes:",
    )
    s.code("""
mean      = average of the 200 outputs                       -> the AI's proposal
s         = sample standard deviation (divide by n - 1)
halfWidth = 1.96 * s                                         -> the 95% band is mean ± halfWidth
interval  = [max(0, mean - halfWidth), min(1, mean + halfWidth)]
""")
    s.p(
        "The 1.96 comes from the normal distribution: about 95% of a normal population lies within 1.96 "
        "standard deviations of the mean. The book's worked example uses exactly this recipe on a "
        "remaining-useful-life prediction (44.2 ± 4.2 days); the POC applies it to a valve opening.",
    )
    s.figure(F["uncertainty_map"], heading="Where the network is sure, and where it is not", caption="Figure 8.1 — 95% half-width of the MC Dropout interval over the operating "
             "envelope. Inside the black contour the network is within the vault's ±0.20 limit; outside it the "
             "command is escalated to a human. S1 and S9 are demo scenarios 1 and 9.")
    m1, s1, h1 = exact_mc(60, 5)
    m9, s9, h9 = exact_mc(85, 7)
    m0, s0, h0 = exact_mc(30, 5)
    s.table([
        ["Reading", "Mean", "Std. dev. s", "95% half-width", "Limit", "Outcome"],
        ["30 °C, 5 bar (tests)", f"{m0:.3f}", f"{s0:.3f}", f"±{h0:.3f}", "±0.20", "Approved"],
        ["60 °C, 5 bar (scenario 1)", f"{m1:.3f}", f"{s1:.3f}", f"±{h1:.3f}", "±0.20", "Approved"],
        ["85 °C, 7 bar (scenario 9)", f"{m9:.3f}", f"{s9:.3f}", f"±{h9:.3f}", "±0.20", "Escalated"],
    ], [0.27, 0.12, 0.14, 0.17, 0.1, 0.2])
    s.p(
        "These values are exact (computed over the sixteen masks). With 200 random passes the POC's estimate "
        "of the half-width wobbles by about ±0.01–0.02, which is why the tests use readings well inside or "
        "well outside the limit. The map shows a pattern that is common in real models: the network is "
        "confident where all its neurons agree — low temperature and moderate pressure — and uncertain where "
        "neurons with opposite signs are both strongly active, which is the hot, high-pressure corner.",
    )
    s.section("The threshold is a fact, not a model setting")
    s.p(
        "The permitted half-width, ±0.20, is not a constant in the code. It is a Static Vault fact — "
        "<i>“maximum ai prediction uncertainty as 95% half-width”</i> — with a version and an approver. This "
        "follows the book's advice in §16.9 about anomaly detection: the <i>score</i> is legitimately "
        "stochastic and model-dependent, but the <i>threshold</i> at which a human must look is an "
        "engineering decision that no amount of retraining should move quietly. Changing it requires a new "
        "version in the vault, exactly like changing a relief setpoint.",
    )
    s.callout("ours", "Uncertainty escalation is our addition", [
        "The book introduces MC Dropout as an evaluation technique (§13.6) and argues for resolving alarm "
        "thresholds through Deterministic Islands (§16.9). Using the interval <i>inside the control loop</i> "
        "— refusing to apply a command whose 95% band is wider than a vault limit and escalating instead — is "
        "this POC's own step. It became Rule 6 of our Neural Constitution (Chapter 20).",
    ])
    s.callout("warn", "What MC Dropout does not tell you", [
        "It measures the model's disagreement with itself, not its distance from the truth. A network that "
        "is confidently wrong — for example on data unlike anything it was trained on — can show a narrow "
        "band. That is why uncertainty is one guard among several, not a replacement for islands and the "
        "Shield.",
    ])

    # ---------------------------------------------------------------- 9
    s.chapter("9", "Frozen Snapshot: the same network, made reproducible",
              "Sometimes you need the model's reasoning and a guarantee that it will give the same answer "
              "tomorrow. The book's §12.3.2 lists exactly what must be locked.")
    s.p(
        "A Static Vault answers questions whose answers can be listed in advance. Some behaviour, however, "
        "must come from the model itself — and still be reproducible bit for bit. The <b>Frozen Snapshot</b> "
        "achieves this by removing every remaining degree of freedom from inference. The book names four, "
        "and the POC's <font face='Mono'>FrozenSnapshotConfig</font> has exactly those four fields:",
    )
    s.table([
        ["Locked item", "Why it matters", "How the POC enforces it"],
        ["Model weights", "Different weights, different answers.", "SHA-256 of all 17 weights; the snapshot is rejected if it does not match."],
        ["Runtime version", "Math libraries can round differently between releases.", "The .NET runtime version is recorded; a mismatch rejects the snapshot."],
        ["Random seed", "Dropout masks must be the same every time.", "A new random generator with the fixed seed is created for <i>every</i> call."],
        ["CPU-only inference", "Parallel sums finish in varying order (see below).", "Single-threaded, fixed summation order; a non-CPU config is rejected."],
    ], [0.18, 0.37, 0.45])
    s.section("Why the order of a sum matters")
    s.figure(F["float"], "Figure 9.1 — The book's §24.2.2 example: in IEEE-754 double precision, (a + b) + c "
             "and a + (b + c) give different answers.", width=0.85)
    s.p(
        "Floating-point addition is not associative. With a = 10<super>16</super>, b = −10<super>16</super> "
        "and c = 1, computing (a + b) + c gives 1, but a + (b + c) gives 0, because 1 is below the "
        "precision available at 10<super>16</super> and disappears in b + c. A GPU sums the many products of "
        "a neural network in parallel, in an order that depends on which thread finishes first; the result can "
        "therefore differ in the last bits from run to run. A single-threaded CPU loop fixes one order. In the "
        "POC this is visible in the code as a plain loop over the hidden neurons with a comment pointing to "
        "§24.2.2.",
    )
    s.section("When the snapshot is used")
    s.p(
        "The Arbiter uses the frozen core only when the operator has engaged the <b>Causality Lock</b> "
        "(Chapter 16) and no island decides the cycle. In that case the ordinary stochastic answer is "
        "forbidden, but a frozen, reproducible answer is acceptable: it sits third in the book's precedence "
        "order, after the Static Vault and a routed vector (§12.4.3). Scenario 6 shows it: the same reading "
        "twice, 41.7% both times, identical to the last bit.",
    )
    s.callout("warn", "Reproducible is not the same as correct", [
        "A Frozen Snapshot guarantees that the model will repeat itself, not that it is right. The book's "
        "Honest Boundary in §24.3 adds a second cost: single-threaded CPU inference can be one to two orders "
        "of magnitude slower for a large model. It is a tool for the narrow class of queries that genuinely "
        "need reproducibility, not a general setting.",
    ])
