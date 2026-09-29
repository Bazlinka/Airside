# 0187 — Fenced passenger walkways

Status: accepted (Unity look not yet verified)

Passengers walking to an aircraft cross the apron in a marked corridor edged with posts and red-and-white barrier tape,
as at a real airport, so they cannot wander onto a taxiway or runway. `AdelaideWalkwayGeometry` (pure, tested) builds one
straight corridor from the terminal wall to 32 m short of each regional bay's nose stop (walks over 260 m are bussed and
not fenced: today 7 of the 12 bays). The tape is drawn with the road props sink; the boarding walk in
`AirsidePrototype.Boarding` now starts at the corridor's terminal end and follows it before the planned route round the
aircraft. Tests: no corridor touches the runway strip, walk lengths, vertex budget (about 22k).

Also checked for the far zoom-out (ADR 0185): the distant-light beacon and field tags have no upper distance limit and the
sky discs sit inside the raised near clip, so arrivals stay findable at 45 km. Still to confirm in Unity.
