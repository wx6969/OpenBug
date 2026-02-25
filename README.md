# OpenBug

OpenBug is a unique desktop companion application that simulates a living colony of digital bugs on your screen. 

More than just a technical showcase, **OpenBug is designed as a lightweight visual companion to help improve focus and aid in attention management.** By providing non-intrusive background movement, it serves as visual "white noise," making it especially suitable for neurodivergent individuals (such as those with ADHD) or anyone who struggles to maintain focus in a completely static digital environment.

Using procedural animation and inverse kinematics (IK), these entities move realistically across your desktop, interacting with each other and their environment without disrupting your workflow.

## 🧠 Focus & Attention Management

For many users, a perfectly quiet and static screen can lead to under-stimulation and distraction. OpenBug is built to counteract this by offering:

* **Visual White Noise:** The organic, unpredictable, yet subtle movement of the bugs provides just enough background stimulation to anchor wandering attention without breaking your flow.
* **Passive "Body Doubling":** The presence of a growing, active digital colony creates a subtle sense of companionship during isolated deep-work sessions.
* **Organic Time Tracking:** The colony's lifecycle (spawning a new bug every 10 minutes) acts as a low-stress, ambient visual timer, helping users mitigate "time blindness" without the anxiety of a ticking clock.

## ⚙️ Overview & Key Features

OpenBug creates a self-sustaining digital ecosystem right on your desktop. The entities are not static images, but procedurally animated creatures with distinct behaviors and a living ecosystem.

* **Procedural Animation:** Bugs use real-time Inverse Kinematics (IK) for leg movement, creating fluid and organic walking animations that adapt dynamically to speed and direction.
* **Dynamic Lifecycle System:**
    * Starts with 5 initial bugs to establish the colony.
    * Spawns 1 new bug every 10 minutes (up to a maximum of 50), acting as a passive Pomodoro-style tracker.
    * **Color Progression:** As the colony grows, new bugs become increasingly vivid and bright, visually simulating a maturing ecosystem.
* **Behavioral Intelligence:**
    * **Social:** Some bugs prefer to stick together in groups (Swarm behavior).
    * **Loner:** Others prefer solitude, moving faster and avoiding crowds.
    * **Edge Dweller:** A specific percentage naturally gravitates towards walking along the very edges of your monitor.
* **Unobtrusive Integration:** Minimized to the system tray. You can easily right-click to "Hide" (pause the simulation instantly) or "Quit".
* **Performance Optimized:** Engineered to run efficiently in the background with near-zero impact on your CPU/GPU, ensuring your actual work remains unaffected.

## 🔬 Bug Behavior & Scientific Inspiration

The movement and behavior of OpenBug are deeply rooted in real-world insect locomotion and swarm intelligence research:

* **Inverse Kinematics (IK):** The leg movement utilizes a 2-bone IK solver to calculate joint angles dynamically. This mimics the biological structure of insect legs, allowing for realistic foot placement and gait adaptation.
    * *Reference:* Aristidou, A., & Lasenby, J. (2011). FABRIK: A fast iterative solver for the Inverse Kinematics problem. *Graphical Models*, 73(5), 243-260.
* **Swarm Rules (Boids):** The social behavior of the bugs is based on Craig Reynolds' classic Boids algorithm, implementing Separation, Alignment, and Cohesion forces to simulate natural flocking and swarming behavior.
    * *Reference:* Reynolds, C. W. (1987). Flocks, herds and schools: A distributed behavioral model. *SIGGRAPH '87 Conference Proceedings*.
* **Procedural Gait:** The walking cycle implements a dynamic tripod gait common in hexapods (insects), ensuring visual stability and realistic movement patterns.
    * *Reference:* Delcomyn, F. (1980). Neural basis of rhythmic behavior in animals. *Science*, 210(4469), 492-498.

## 🚀 Installation & Usage

1.  Download the latest `OpenBug.exe` from the Releases page.
2.  Run the executable. No installation required.
3.  The application will quietly start and reside in your system tray.
4.  Right-click the tray icon to:
    * **Hide:** Instantly hides all bugs and pauses the spawning simulation (perfect for screen sharing).
    * **Quit:** Exits the application entirely.

## 📄 License

This project is licensed under the MIT License.