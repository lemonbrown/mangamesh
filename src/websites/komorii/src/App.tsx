import { BrowserRouter, Routes, Route } from 'react-router-dom';
import Layout from './components/Layout';
import Series from './pages/Series';
import SeriesDetails from './pages/SeriesDetails';
import Reader from './components/Reader';

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Layout />}>
          <Route index element={<Series />} />
          <Route path="series/:seriesId" element={<SeriesDetails />} />
        </Route>
        {/* Reader is outside of layout to support full screen black background */}
        <Route path="/series/:seriesId/read/:chapterId" element={<Reader />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
