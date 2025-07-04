# 🚲 AI Bicycle Balance Training 

An AI learns to ride and balance a **bicycle** through 6 progressively difficult stages using Unity ML-Agents and reinforcement learning.

## What It Does

Trains an AI agent to **balance and ride a bicycle** across increasingly challenging terrains:

1. **Basic Balance** – Maintain upright posture while riding straight  
2. **Speed Bumps** – Navigate over bumps and dips  
3. **Tightrope + Bumps** – Balance on narrow paths with interruptions  
4. **Downhill** – Handle gravity and speed on declines  
5. **Uphill** – Apply power and control climbing  
6. **Combined Challenge** – A composite level that includes all the above elements in one course

## Technologies & Techniques

- **Game Engine**: Unity 2022.3+ with ML-Agents Toolkit  
- **AI Training**: PPO (Proximal Policy Optimization), GAIL, Behavioral Cloning  
- **3D Modeling & Rigging**: Designed using Blender  
- **Animation & Physics**: Rigged bicycle model with realistic physics-based balancing  
- **Machine Learning**: Imitation + Reinforcement Learning (Python 3.8+, PyTorch backend)  
- **Curriculum Learning**: Environment complexity scales over time for smoother convergence  

## Training Progress

The AI was trained for **over 1.6 million steps** using PPO. Below is the **actual cumulative reward** graph captured from TensorBoard during the training process:

<img width="344" height="250" alt="Cumulative Reward" src="https://github.com/user-attachments/assets/fe616dd7-e59e-405f-a607-44dcb9013cde" />

- 📉 The cumulative reward initially declined as the agent explored and failed frequently  
- 🔄 Between 600k and 1.4M steps, the agent started stabilizing across levels with moderate gains  
- 📈 After 1.6M steps, the agent achieved consistent high rewards, exceeding **240 cumulative reward**, indicating strong and reliable bicycle balance and navigation  

## Core Files 📁

- `BicycleController.cs` – Handles physics and control of the bicycle  
- `AgentBicycle.cs` – ML-Agent logic: observations, actions, and rewards  
- `bicycle_config.yaml` – PPO hyperparameters and curriculum configurations
