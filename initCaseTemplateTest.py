from gpt4all import GPT4All
import time
import os
model = GPT4All('mistral-7b-instruct-v0.1.Q4_0.gguf', allow_download=False, n_threads=490)
sysPrompt = ""
with open("C:\\testenv\\case\\Case\\Case\\bin\\Debug\\initCase.Conversational.txt", "r") as file:
	sysPrompt = file.read().replace("\n", "")
clear = lambda: os.system('cls')
clear()
model_load_time = time.time()
with model.chat_session():
	initModelResponse = model.generate(sysPrompt)
	print("%s" % (time.time() - model_load_time))
	while (True):
		consoleInput = input(f"UserPrompt>")
		clear()
		if consoleInput == "exit":
			break
		else:
			start_time = time.time()
			response = model.generate(consoleInput)
			print("--- %s seconds ---" % (time.time() - start_time))
			print(response)
			print("--Done--")