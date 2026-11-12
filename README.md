# reference_application

This repository hosts the c# reference application for the PIQI framework

## Table of Contents

- [About](#about)
- [Purpose](#purpose)
- [Features](#features)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Installation](#installation)
- [Usage](#usage)
- [Contributing](#contributing)
- [License](#license)
- [Contact](#contact)

## About

Patient Information Quality Improvement (PIQI) is an emerging open framework for evaluating the quality of electronic 
patient data. It aims to enhance the usability of shared patient information by ensuring it meets specific
criteria for accuracy, conformity, availability, and plausibility. PIQI assesses data against a standard,
such as USCDI v3, generates a scorecard, and provides insights into issues affecting the quality score. 
This feedback enables data sources to make necessary adjustments to meet quality requirements. 

## Purpose

This code is intended to serve as a **reference guide** for implementing the PIQI framework. The primary goal was to provide clear and understandable examples rather than optimizing for
performance.

## Features

- Allows users to evaluate the quality of electronic patient data.
- Takes input data in PIQI format.
- Uses FHIR terminology services for validation.
- Generates a quality scorecard and insights.
- Provides a foundation for building more complex applications.
- Easily extensible for additional models and rubrics.

## Getting Started

### Prerequisites

List things that are prerequisites to setting up the project.

- .NET SDK 8.0 or later
- Visual Studio or other IDEs

### Installation

1. Clone the repository:

    ```sh
    git clone https://github.com/piqiframework/reference_application.git
    ```

2. Navigate into the project directory:

    ```sh
    cd reference_application
    ```

3. Restore dependencies:

    ```sh
    dotnet restore
    ```
4. Add FHIR terminology services configuration in `appsettings.json` (*Note: example code does not support authentication for FHIR terminology service*):
    ```json
    {
      "Fhir": {
        "BaseUrl": "<YOUR FHIR SERVER URL GOES HERE>"
      }
    }
    ```

5. Build the project:

    ```sh
    dotnet build
    ```

6.  Run the Application (This will start the web service, currently configured to run on port 44398 `https://localhost:44398/`):

    ```sh
    dotnet run
    ```

### Running in Docker Compose

1. Copy `.env.example` to `.env` and modify it for your particular terminology server.

2. If you want to run the server on a port besides 8080, also specify a value for `PIQI_PORT`.

```
# Environment variables to be used for docker compose.
# FHIR_BASE_URL represnts the endpoint for the FHIR
# terminology server.
#FHIR_BASE_URL=https://tx.fhir.org/r4/
FHIR_BASE_URL=http://hapi.fhir.org/baseR4/
PIQI_PORT=8090
```

3. Start the orchestrated Docker containers with the `docker compose up --build` command.

4. Stop the containers with `docker compose down`.

## Usage

### Using Swagger UI

Once the application is running, you can interact with the API using the built-in Swagger UI.

1. **Navigate to Swagger:**

   Open your web browser and go to:

   ```
   https://localhost:44398/swagger
   ```

2. **Explore Endpoints:**

   The Swagger page will display all available endpoints, allowing you to test them directly from the UI. You can
view detailed information about each endpoint, including parameters, request bodies, and response formats.

3. **Test Endpoints:**

   - Select an endpoint from the list.
   - Click on "Try it out" to expand the options.
   - Fill in any required parameters or request body fields.
   - Click "Execute" to send a request and view the response directly in your browser.

### Calling the API Through Code

You can also call the web service programmatically using HTTP clients available in various programming languages.


## Contributing

Contributions are what make the open-source community such an amazing place to be learn, inspire, and create. Any
contributions you make are **greatly appreciated**. Please read our contributing guidelines to get started.

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

Distributed under the Apache 2.0 License. See `LICENSE` for more information.

## Contact

Project Link: [https://github.com/piqiframework](https://github.com/piqiframework)