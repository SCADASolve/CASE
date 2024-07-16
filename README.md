# CASE

CASE or Computer Assisted Systems Engineer is an application that leverages Large Language Models (LLM) and Robotic Process Automation (RPA) to automate various tasks. It can monitor databases, manage commands, and execute actions based on specified triggers. This README provides an overview of the available commands and their functionalities.

More information available at [here](http://www.scadasolve.com/case/).

## Table of Contents

- [Installation](#installation)
- [Usage](#usage)
  - [Commands](#commands)
    - [`-talk`](#-talk)
    - [`-analyze`](#-analyze)
    - [`-command`](#-command)
    - [`-execute`](#-execute)
    - [`-monitor`](#-monitor)
    - [`-settings`](#-settings)
    - [`-resetSettings`](#resetsettings)
    - [`-help`](#help)
- [License](#license)

## Installation

To install Case, clone the repository and build the project using your preferred IDE or .NET CLI:

```sh
git clone https://github.com/SCADASolve/case.git
cd case
dotnet build
```
## Usage

### Commands

#### `-command <sub-command> [options]`

Manages RPA commands.

- **Sub-commands:**
  - `-talk`: Initiates the Conversational Agent via Python.
  - `-analyze`: Use the Generative AI to analyze files and databases.
  - `-create`: Starts the interactive creation process.
  - `-update`: Explains the update process for each command.
  - `-view`: Views available commands.
    - `-view <keywords>`: Filters commands by software.
  - `-help`: Displays a basic overview of RPA commands and options.
  - `-import <filePath>`: Imports commands from a specified `.case` file.
  - `-export`: Exports the selected commands to a `.case` file.
  - `<keywords>`: Detects and executes commands based on keywords.
    - Options:
      - `-p`: Automatically picks the first detected command.
      - `-v`: Bypasses command validation.
      - `-e`: Bypasses command explanation.

#### `-talk`

Starts the Conversational Generative AI agent.

- **Example:**
  ```sh
  -talk

#### `-analyze <type> <data> <prompt>`

Analyzes the specified data.

- **Types:**
  - `-file`: Analyzes a file.
    - **Example:**
      ```sh
      -analyze -file "path/to/file.txt" "analyze this file"
      ```
  - `-database`: Analyzes a database query.
    - **Example:**
      ```sh
      -analyze -database "SELECT * FROM table" "analyze this query"
      ```

#### `-execute "<command>"`

Executes the specified RPA directly.

- **Example:**
  ```sh
  -execute e;sExplorer.exe{ENTER}
  ```
#### `-monitor <type> <data1> [data2] <command>`

Begins monitoring with the provided type and data.

- **Types:**
  - `-image`: Monitors the screen for an image.
    - **Example:**
      ```sh
      -monitor -image "path/to/image.jpg" "some command"
      ```
  - `-file`: Monitors the filesystem for a file.
    - **Example:**
      ```sh
      -monitor -file "file.txt" "directory" "some command"
      ```
  - `-database`: Monitors a SQL table for a condition.
    - **Example:**
      ```sh
      -monitor -database "table" "condition" "some command"
      ```
  - `-c`: Monitors the clipboard for a defined command.
    - **Example:**
      ```sh
      -monitor -c
      ```

#### `-settings <sub-command> [options]`

Manages application settings.

- **Sub-commands:**
  - `-unlock`: Unlocks the settings for modification.
    - **Example:**
      ```sh
      -settings -unlock
      ```
  - `-password`: Manages passwords (Requires unlock).
    - Sub-commands:
      - `-c`: Creates a new encrypted password file.
        - **Example:**
          ```sh
          -settings -password -c
          ```
      - `-k`: Checks the entered password against the stored password file.
        - **Example:**
          ```sh
          -settings -password -k
          ```
      - `-h`: Changes the password.
        - **Example:**
          ```sh
          -settings -password -h
          ```
  - `-update <setting:value>`: Updates a setting to the given value (Requires unlock).
    - **Example:**
      ```sh
      -settings -update "SettingName:True"
      ```
  - `-show`: Displays current settings for Case.
    - **Example:**
      ```sh
      -settings -show
      ```
  - `-gpu`: Lists and allows selection of a GPU for Generative AI configuration.
    - **Example:**
      ```sh
      -settings -gpu
      ```

#### `-resetSettings`

Deletes settings file to be re-initialized through the 'InitialSetup' process.

- **Example:**
  ```sh
  -resetSettings

#### `-help`

Displays the help message.

- **Example:**
  ```sh
  -help

## License

This project is licensed under the GPLv3 License for non-commercial use, for commercial use, please see https://scadasolve.com/case/licensing for details. See the [LICENSE](LICENSE) file for details.
