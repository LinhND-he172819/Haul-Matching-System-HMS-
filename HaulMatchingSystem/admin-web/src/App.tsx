import { useState } from 'react';
import DriverTripsPage from './pages/DriverTripsPage';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import HomePage from './pages/HomePage';

type Page = 'login' | 'register' | 'home';

function App() {
  const [currentPage, setCurrentPage] = useState<Page>('login');

  const handleNavigate = (page: Page) => {
    setCurrentPage(page);
  };

  return (
    <>
      {currentPage === 'login' && <LoginPage onNavigate={handleNavigate} />}
      {currentPage === 'register' && <RegisterPage onNavigate={handleNavigate} />}
      {currentPage === 'home' && <HomePage onNavigate={handleNavigate} />}
    </>
  );
}

export default App;
