# AppointPro

AppointPro is a comprehensive and user-friendly web application designed to streamline the process of booking and managing medical appointments. It provides a platform for patients to find doctors, book appointments, and make payments seamlessly.

## Features

-   **User Authentication:** Secure user registration and login using Google OAuth.
-   **Search Functionality:** Easily search for doctors and hospitals based on various criteria.
-   **Appointment Booking:** A simple and intuitive interface for booking appointments with doctors.
-   **Online Payments:** Integrated with Stripe for secure online payments.
-   **Email Notifications:** Automated email notifications for appointment confirmations.
-   **Responsive Design:** A responsive user interface that works on both desktop and mobile devices.

## Technologies Used

-   **Backend:** ASP.NET Core MVC, Entity Framework Core
-   **Database:** SQL Server
-   **Payments:** Stripe API
-   **Authentication:** Google OAuth
-   **Email:** SMTP

## Getting Started

Follow these instructions to get a copy of the project up and running on your local machine for development and testing purposes.

### Prerequisites

-   .NET 8 SDK
-   SQL Server
-   A Stripe account with API keys
-   A Google Cloud Platform project with OAuth 2.0 credentials

### Installation

1.  **Clone the repository:**
    ```sh
    git clone https://github.com/your-username/AppointPro.git
    cd AppointPro
    ```

2.  **Configure the application:**
    Open `AppointPro/appsettings.json` and update the following sections:
    -   `ConnectionStrings`: Set your SQL Server connection string.
    -   `Stripe`: Add your Stripe secret key and publishable key.
    -   `Google`: Add your Google client ID and client secret.
    -   `SmtpSettings`: Configure your SMTP server settings for sending emails.

3.  **Set up the database:**
    Run the following commands to apply the database migrations:
    ```sh
    dotnet ef database update
    ```

4.  **Run the application:**
    ```sh
    dotnet run
    ```
    The application will be available at `https://localhost:5001`.

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.
