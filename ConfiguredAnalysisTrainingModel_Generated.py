from gpt4all import GPT4All
import time
import os
model = GPT4All('mistral-7b-instruct-v0.1.Q4_0.gguf', allow_download=False, n_threads=490, device='NVIDIA GeForce RTX 2070 SUPER')
sysPrompt = ""
with open("C:\\testenv\\case\\Case\\Case\\bin\\Debug\\chunk_1.txt", "r") as file:
	sysPrompt = file.read().replace("\n", "")
start_time = time.time()
with model.chat_session():
	initModelResponse = model.generate(sysPrompt)
	print(initModelResponse)
	print("--Done--")
	while (True):
		consoleInput = input(f"UserPrompt>")
		if consoleInput == "exit":
			break
		else:
			start_time = time.time()
			response = model.generate(consoleInput)
			print("--- %s seconds ---" % (time.time() - start_time))
			print(response)
			print("--Done--")