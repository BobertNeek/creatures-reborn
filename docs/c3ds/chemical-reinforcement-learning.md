# Chemical Reinforcement Learning

Chemical reinforcement is the bridge between body state and learning. The reinforcement bus reads chemical deltas and produces typed, multi-channel signals instead of collapsing life into one scalar score.

Signal domains include energy, hunger, pain, fear/stress, comfort, fatigue, social pressure, learning chemicals, and health. Examples:

- Falling hunger after eating is positive hunger reinforcement.
- ATP recovery is positive energy reinforcement.
- Rising pain, injury, fear, or punishment is negative reinforcement.
- Falling loneliness is positive social reinforcement.

Classic mode remains unchanged. Chemical signals are recorded into `LearningTrace` only when chemical learning is explicitly requested through trace options or future training modes.
