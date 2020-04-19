# Demonstration of boid agents and its flocking behaviors powered by Unity C# Job System.
This Demonstration does NOT utilize the Unity's Entities from their Enitity Component System (ECS) as my use case required non-entities. Each agent is a regular gameobject that uses regular Unity/Nvidia physics. However, since design is similar to the data oriented architecture, it should be easy to transition to a ECS system.

In this project, I created a simulation of fish schools inside a contained area. Each fish is a gameobject and must loop through each other fish to calculate its next trajectory. To achieve maxium performance, we utilize Unity C# job systems to multithread each fish's calculations.

In the end, performance and results was very good. I was able to get more then 90 frames a second with thousands of agents which was the minimum target when tested inside a VR project.

# Whats Next?
* A solution to alleviate processing through every other fish is to only process fish only ones near it.
* Avoidance steering such as from sharks when approached too close.

This approach does NOT make use of Unity's Entities from Unity's ECS.

# References:
* https://en.wikipedia.org/wiki/Boids
* https://docs.unity3d.com/Manual/JobSystem.html

# Demos & samples:
