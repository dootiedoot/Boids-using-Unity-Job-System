# Demonstration of boid agents and its flocking behaviors powered by Unity C# Job System.
This Demonstration does NOT utilize the Unity's Entities from their Enitity Component System (ECS) as my use case required non-entities. Each agent is a regular gameobject that uses regular Unity/Nvidia physics. However, since design is similar to the data oriented architecture, it should be easy to transition to a ECS system.

In this project, I created a simulation of fish schools inside a contained area. Each fish is a gameobject and must loop through each other fish to calculate its next trajectory. To achieve maxium performance, we utilize Unity C# job systems to multithread each fish's calculations.

In the end, performance and results was very good. I was able to get more then 90 frames a second with thousands of agents which was the minimum target when tested inside a VR project.

# References:
* https://en.wikipedia.org/wiki/Boids
* https://docs.unity3d.com/Manual/JobSystem.html

# Demos & samples:
[![](http://img.youtube.com/vi/dEJ2Ut7DKr0/0.jpg)](http://www.youtube.com/watch?v=dEJ2Ut7DKr0)

[![2](http://img.youtube.com/vi/T1JvPYZJz_g/0.jpg)](http://www.youtube.com/watch?v=T1JvPYZJz_g)

[![3](http://img.youtube.com/vi/gbICBO0Wpxc/0.jpg)](http://www.youtube.com/watch?v=gbICBO0Wpxc)

# Whats Next?
* Currently, each agent has to loop through every other agent even if the other agent is too far for relevance. 
* * Solution to this is to have each agent only calculate only its neighboring agents by caching neighbors within a sphere.
* Add avoidance steering such from agents such as sharks when approached too close.
* * Solution: Add influence logic and parameters to affect steering force.
